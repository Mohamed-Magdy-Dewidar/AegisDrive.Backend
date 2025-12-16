using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Events;
using AegisDrive.Api.Contracts.RealTime;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Features.Drivers;
using AegisDrive.Api.Features.Fleet;
using AegisDrive.Api.Features.Monitoring;
using AegisDrive.Api.Features.SafetyEvents;
using AegisDrive.Api.Hubs;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Email;
using Amazon.SQS;
using Amazon.SQS.Model;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Ingestion.Consumers;

public class SafetyEventSqsConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SafetyEventSqsConsumer> _logger;
    private readonly string _queueUrl;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redisMux;
    private readonly IHubContext<FleetHub, IFleetClient> _hubContext; // [NEW] SignalR Context

    public SafetyEventSqsConsumer(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> sqsSettings,
        ILogger<SafetyEventSqsConsumer> logger,
        IConnectionMultiplexer redisMux,
        IServiceProvider serviceProvider,
        IHubContext<FleetHub, IFleetClient> hubContext) // [NEW] Injected here
    {
        _sqsClient = sqsClient;
        _queueUrl = sqsSettings.Value.DrowsinessEventsQueueUrl;
        _logger = logger;
        _redisMux = redisMux;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🛡️ Safety Event (High/Medium) SQS Consumer started: {Queue}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var sqsMessage in response.Messages ?? new List<Message>())
                {
                    if (await ProcessMessageAsync(sqsMessage, stoppingToken))
                    {
                        await _sqsClient.DeleteMessageAsync(_queueUrl, sqsMessage.ReceiptHandle, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error polling Safety Events queue");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessMessageAsync(Message sqsMessage, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("📩 Received Event: MessageId={MessageId}", sqsMessage.MessageId);

            var message = JsonSerializer.Deserialize<DrowsinessEventMessage>(
                sqsMessage.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (message == null) return false;

            using var scope = _serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var safetyRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SafetyEvent, Guid>>();
            var notificationService = scope.ServiceProvider.GetRequiredKeyedService<INotificationService>("Email");
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var redis = _redisMux.GetDatabase();

            // 1. Idempotency
            if (await safetyRepository.AnyAsync(sf => sf.Id == message.EventId, token)) return true;

            // 2. Fetch Data
            var vehicleResult = await sender.Send(new GetVehicle.Query(message.VehicleId), token);
            if (vehicleResult.IsFailure || vehicleResult.Value.CurrentDriverId == null) return true;
            var vehicleData = vehicleResult.Value;
            var driverId = vehicleData.CurrentDriverId.Value;

            var driverResult = await sender.Send(new GetDriverProfile.Query(driverId), token);
            var driverProfile = driverResult.Value;

            // 3. Live Context
            var liveState = await sender.Send(new GetVehicleLiveState.Query(message.VehicleId), token);
            double speed = liveState.Value?.LiveLocation?.SpeedKmh ?? 0;
            string mapLink = liveState.Value?.LiveLocation != null
                ? GpsLinkUtility.GenerateMapsLink(liveState.Value.LiveLocation.Latitude, liveState.Value.LiveLocation.Longitude)
                : "Location Unavailable";

            // 4. Save to DB
            Enum.TryParse<AlertLevel>(message.AlertLevel, true, out var alertLevel);
            Enum.TryParse<DriverState>(message.DriverState, true, out var driverState);
            DateTime eventTimestamp = message.GetParsedTimestamp();

            var createCommand = new CreateSafetyEvent.Command(
                message.EventId, message.Message,
                message.EarValue, message.MarValue, message.HeadYaw,
                driverState, alertLevel,
                message.S3DriverImagePath, message.S3RoadImagePath,
                message.RoadStatus?.HasHazard ?? false,
                message.RoadStatus?.VehicleCount ?? 0,
                message.RoadStatus?.PedestrianCount ?? 0,
                message.RoadStatus?.ClosestObjectDistance,
                eventTimestamp, message.DeviceId, message.VehicleId,
                driverId, vehicleData.CompanyId
            );

            var saveResult = await sender.Send(createCommand, token);
            if (saveResult.IsFailure) return false;

            _logger.LogInformation("💾 Saved {Level} Event {Id}", alertLevel, message.EventId);
            var safetyData = saveResult.Value;

            // 5. Update Score
            await sender.Send(new DeductDriverSafteyScore.Command(driverId, alertLevel), token);



            // =========================================================
            // 🚀 PARALLEL EXECUTION (SignalR & Email)
            // =========================================================

            var signalRTask = Task.Run(() => SendSignalRAlertAsync(message, vehicleData, safetyData, fileStorageService, mapLink, speed, alertLevel));

            Task emailTask = Task.CompletedTask;
            if (driverProfile?.DriverCompany != null && !string.IsNullOrEmpty(driverProfile.DriverCompany.RepresentativeEmail))
            {
                 emailTask = Task.Run(() => SendEmailNotificationsAsync(message, vehicleData, driverProfile, notificationService, redis, alertLevel));
            }
            await Task.WhenAll(signalRTask, emailTask);

            return true;


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error processing safety event");
            return false;
        }
    }


    private async Task SendSignalRAlertAsync(
        DrowsinessEventMessage message,
        AegisDrive.Api.Contracts.Vehicles.GetVehicleResponse vehicleData,
        AegisDrive.Api.Contracts.SafetyEventsDto.CreatedSafetyEventResponse safetyData,
        IFileStorageService fileService,
        string mapLink,
        double speed,
        AlertLevel level)
    {
        try
        {
            // Both HIGH and MEDIUM events get pushed to the dashboard
            // The Frontend uses "AlertLevel" to decide if it's Red (High) or Orange (Medium)
            string driverImgUrl = fileService.GetPresignedUrl(safetyData.DriverImageKey ?? "");

            var alertNotification = new HighAlertNotification(
                message.EventId,
                vehicleData.PlateNumber,
                message.DriverState,
                level.ToString(),
                message.Message,
                mapLink,
                speed,
                DateTime.UtcNow,
                driverImgUrl
            );

            if (vehicleData.CompanyId.HasValue)
            {
                var group = $"company_{vehicleData.CompanyId}".ToLower();
                await _hubContext.Clients.Group(group).ReceiveHighLevelAlert(alertNotification);
                _logger.LogInformation("📡 SignalR {Level} Alert sent to Company: {Group}", level, group);
            }
            else if (!string.IsNullOrEmpty(vehicleData.OwnerUserId))
            {
                // "user_0ded8209-fe72-4c18-ae10-0fdbbaf727c8"
                var group = $"user_{vehicleData.OwnerUserId}".ToLower();
                await _hubContext.Clients.Group(group).ReceiveHighLevelAlert(alertNotification);
                _logger.LogInformation("📡 SignalR {Level} Alert sent to User: {Group}", level, group);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ SignalR Task Failed for Event {EventId}", message.EventId);
        }
    }

    private async Task SendEmailNotificationsAsync(
        DrowsinessEventMessage message,
        AegisDrive.Api.Contracts.Vehicles.GetVehicleResponse vehicleData,
        AegisDrive.Api.Contracts.Drivers.GetDriverProfileResponse? driverProfile,
        INotificationService notificationService,
        IDatabase redis,
        AlertLevel level)
    {
        try
        {
            // ONLY send emails for HIGH alerts
            if (level != AlertLevel.HIGH) return;

            // Rate Limit Check
            string cooldownKey = $"email_high_cooldown:{vehicleData.CurrentDriverId}";
            if (!await redis.StringSetAsync(cooldownKey, "1", TimeSpan.FromMinutes(1), When.NotExists))
            {
                _logger.LogInformation("⏳ Email skipped (Cooldown active) for driver {DriverId}", vehicleData.CurrentDriverId);
                return;
            }

            // Send to Company
            if (driverProfile?.DriverCompany != null && !string.IsNullOrEmpty(driverProfile.DriverCompany.RepresentativeEmail))
            {
                await notificationService.SendHighAlertAsync(
                    driverProfile.DriverCompany.RepresentativeEmail,
                    driverProfile.FullName,
                    vehicleData.PlateNumber,
                    message.Message,
                    message.DriverState,
                    message.EventId,
                    message.DeviceId
                );
                _logger.LogInformation("📧 High Alert Email sent to Company.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Email Task Failed for Event {EventId}", message.EventId);
        }
    }
}