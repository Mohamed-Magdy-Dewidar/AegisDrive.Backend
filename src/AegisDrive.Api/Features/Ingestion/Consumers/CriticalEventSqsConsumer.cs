using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Events;
using AegisDrive.Api.Contracts.RealTime;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Features.Drivers;
using AegisDrive.Api.Features.Monitoring;
using AegisDrive.Api.Features.SafetyEvents;
using AegisDrive.Api.Features.Vehicles;
using AegisDrive.Api.Hubs;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Services;
using Amazon.SQS;
using Amazon.SQS.Model;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Ingestion.Consumers;

public class CriticalEventSqsConsumer : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<CriticalEventSqsConsumer> _logger;
    private readonly string _queueUrl;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redisMux;
    private readonly IHubContext<FleetHub, IFleetClient> _hubContext;
    
    public CriticalEventSqsConsumer(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> SqsSettings,
        IConfiguration configuration,
        ILogger<CriticalEventSqsConsumer> logger,
        IConnectionMultiplexer redisMux,
        IServiceProvider serviceProvider,
        IHubContext<FleetHub, IFleetClient> hubContext)
    {
        _sqsClient = sqsClient;
        _queueUrl = SqsSettings.Value.DrowsinessCriticalEventsQueueUrl;
        _logger = logger;
        _redisMux = redisMux;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext; 
    }
                  

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Critical Event SQS Consumer started for queue: {Queue}", _queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 5, // Process one critical event at a time
                    WaitTimeSeconds = 20,     // Long polling
                    MessageAttributeNames = new List<string> { "All" },
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var sqsMessage in response.Messages ?? new List<Message>())
                {
                    var processSuccess = await ProcessMessageAsync(sqsMessage, stoppingToken);

                    if (processSuccess)
                    {
                        // Delete message from queue after successful processing
                        await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                        {
                            QueueUrl = _queueUrl,
                            ReceiptHandle = sqsMessage.ReceiptHandle
                        }, stoppingToken);

                        _logger.LogInformation("✅ Message {MessageId} processed and deleted from queue",
                            sqsMessage.MessageId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Message {MessageId} processing failed, will retry",
                            sqsMessage.MessageId);
                        // Message stays in queue and will be retried after visibility timeout
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error polling Critical Events SQS queue");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("🛑 Critical Event SQS Consumer stopped");
    }

    private async Task<bool> ProcessMessageAsync(Message sqsMessage, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("📩 Received Critical Event: MessageId={MessageId}", sqsMessage.MessageId);

            // Deserialize message body directly (no envelope wrapper)
            var message = JsonSerializer.Deserialize<CriticalDrowsinessEventMessage>(
                sqsMessage.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                }
            );

            if (message == null)
            {
                _logger.LogError("❌ Failed to deserialize message {MessageId}", sqsMessage.MessageId);
                return false; // Don't delete - needs investigation
            }

            _logger.LogInformation("🔥 Processing Critical Event: EventId={EventId}, State={State}, AlertLevel={AlertLevel}",
                message.EventId, message.DriverState, message.AlertLevel);


            // Create a scope to get scoped services
            using var scope = _serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var safetyRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SafetyEvent, Guid>>();
            var notificationService = scope.ServiceProvider.GetRequiredKeyedService<INotificationService>("Email");
            var _fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var redis = _redisMux.GetDatabase();


            // 1. Idempotency Check
            if (await safetyRepository.AnyAsync(sf => sf.Id == message.EventId, token))
            {
                _logger.LogInformation("⏭️ Event {EventId} already processed. Skipping.", message.EventId);
                return true; // Already processed, safe to delete
            }

            // Parse timestamp
            DateTime eventTimestamp = message.GetParsedTimestamp();

            // 2. Get Vehicle Info
            var vehicleResult = await sender.Send(new GetVehicle.Query(message.VehicleId), token);
            if (vehicleResult.IsFailure || vehicleResult.Value == null)
            {
                _logger.LogError("❌ Vehicle {VehicleId} not found.", message.VehicleId);
                return false; // Don't delete - might be temporary issue
            }
            var vehicleData = vehicleResult.Value;

            if (!vehicleData.CurrentDriverId.HasValue)
            {
                _logger.LogError("❌ Vehicle {VehicleId} has no active driver.", message.VehicleId);
                return false;
            }

            // 3. Get Driver Profile
            var driverProfileResult = await sender.Send(
                new GetDriverProfile.Query(vehicleData.CurrentDriverId.Value), token);
            if (driverProfileResult.IsFailure)
            {
                _logger.LogError("❌ Driver {DriverId} not found.", vehicleData.CurrentDriverId);
                return false;
            }
            var driverProfile = driverProfileResult.Value;

            // 4. Get Live Location
            double speed = 0;
            string mapLink = "Location Unavailable";

            var vehicleLiveState = await sender.Send(new GetVehicleLiveState.Query(message.VehicleId), token);
            if (vehicleLiveState.IsSuccess && vehicleLiveState.Value?.LiveLocation != null)
            {
                var loc = vehicleLiveState.Value.LiveLocation;
                speed = loc.SpeedKmh;
                mapLink = GpsLinkUtility.GenerateMapsLink(loc.Latitude, loc.Longitude);
            }

            // 5. Save Event (Parse Enums)
            Enum.TryParse<DriverState>(message.DriverState, true, out var driverState);
            Enum.TryParse<AlertLevel>(message.AlertLevel, true, out var alertLevel);

            var createCommand = new CreateCriticalSafetyEvent.Command(
                message.EventId,
                message.Message,
                message.EarValue,
                message.MarValue,
                message.HeadYaw,
                driverState,
                alertLevel,
                message.S3DriverImagePath,
                message.S3RoadImagePath,
                message.RoadStatus?.HasHazard ?? false,
                message.RoadStatus?.VehicleCount ?? 0,
                message.RoadStatus?.PedestrianCount ?? 0,
                message.RoadStatus?.ClosestObjectDistance,
                eventTimestamp,
                message.DeviceId,
                message.VehicleId,
                vehicleData.CurrentDriverId,
                vehicleData.CompanyId
            );


            var SafetyEventSaveResult = await sender.Send(createCommand, token);
            if (SafetyEventSaveResult.IsFailure)
            {
                _logger.LogError("❌ Failed to save safety event {EventId}", message.EventId);
                return false;
            }
            _logger.LogInformation("💾 Safety event {EventId} saved successfully", message.EventId);
            var safetyData = SafetyEventSaveResult.Value;
            
            var signalRTask = Task.Run(() => SendSignalRAlertAsync(
                message, vehicleData, safetyData, _fileStorageService, mapLink, speed));

            var emailTask = Task.Run(() => SendEmailNotificationsAsync(
                message, vehicleData, driverProfile, safetyData, redis, notificationService, _fileStorageService, mapLink, speed));

            await Task.WhenAll(signalRTask, emailTask);
            
            _logger.LogInformation("✅ Critical Event {EventId} processed successfully", message.EventId);
            return true;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "❌ JSON deserialization error for message {MessageId}. Body: {Body}",
                sqsMessage.MessageId, sqsMessage.Body);
            return false; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error processing message {MessageId}", sqsMessage.MessageId);
            return false; 
        }
    }

    
    private async Task SendSignalRAlertAsync(
        CriticalDrowsinessEventMessage message,
        AegisDrive.Api.Contracts.Vehicles.GetVehicleResponse vehicleData ,
        AegisDrive.Api.Contracts.SafetyEventsDto.CreatedCriticalSafetyEventResponse safetyData,
        IFileStorageService fileService,
        string mapLink,
        double speed)
    {
        try
        {
            string driverImgUrl = fileService.GetPresignedUrl(safetyData.DriverImageKey ?? "");

            var alertNotification = new CriticalAlertNotification(
                message.EventId,
                vehicleData.PlateNumber,
                message.DriverState,
                message.AlertLevel,
                message.Message,
                mapLink,
                speed,
                DateTime.UtcNow,
                driverImgUrl
            );

            if (vehicleData.CompanyId.HasValue)
            {
                var group = $"company_{vehicleData.CompanyId}".ToLower();
                await _hubContext.Clients.Group(group).ReceiveCriticalAlert(alertNotification);
                _logger.LogInformation("📡 SignalR sent to Company: {Group}", group);
            }
            else if (!string.IsNullOrEmpty(vehicleData.OwnerUserId))
            {
                var group = $"user_{vehicleData.OwnerUserId}".ToLower();
                await _hubContext.Clients.Group(group).ReceiveCriticalAlert(alertNotification);
                _logger.LogInformation("📡 SignalR sent to User: {Group}", group);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ SignalR Task Failed for Event {EventId}", message.EventId);
        }
    }

    
    private async Task SendEmailNotificationsAsync(
        CriticalDrowsinessEventMessage message,
        AegisDrive.Api.Contracts.Vehicles.GetVehicleResponse vehicleData,
        AegisDrive.Api.Contracts.Drivers.GetDriverProfileResponse? driverProfile,
        AegisDrive.Api.Contracts.SafetyEventsDto.CreatedCriticalSafetyEventResponse safetyData,
        IDatabase redis,
        INotificationService notificationService,
        IFileStorageService fileService,
        string mapLink,
        double speed)
    {
        try
        {
            // Redis Rate Limit Check
            string cooldownKey = $"email_cooldown:{vehicleData.CurrentDriverId}";
            if (!await redis.StringSetAsync(cooldownKey, "sent", TimeSpan.FromSeconds(30), When.NotExists))
            {
                _logger.LogInformation("⏳ Email skipped (Cooldown active) for driver {DriverId}", vehicleData.CurrentDriverId);
                return;
            }

            // Send to Company
            if (driverProfile?.DriverCompany != null && !string.IsNullOrEmpty(driverProfile.DriverCompany.RepresentativeEmail))
            {
                await notificationService.SendCriticalAlertAsync(
                    driverProfile.DriverCompany.RepresentativeEmail,
                    driverProfile.FullName,
                    vehicleData.PlateNumber,
                    message.Message,
                    message.DriverState,
                    mapLink,
                    message.EventId,
                    speed,
                    message.DeviceId
                );
            }

            // Send to Family
            if (driverProfile?.DriverFamilyMembers != null)
            {
                foreach (var family in driverProfile.DriverFamilyMembers.Where(f => f.NotifyOnCritical && !string.IsNullOrEmpty(f.Email)))
                {
                    await notificationService.SendCriticalAlertAsync(
                        family.Email, driverProfile.FullName, vehicleData.PlateNumber,
                        message.Message, message.DriverState, mapLink,
                         message.EventId, speed,  message.DeviceId
                    );
                }
            }
            _logger.LogInformation("📧 Email notifications sent for Event {EventId}", message.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Email Task Failed for Event {EventId}", message.EventId);
        }
    }




}