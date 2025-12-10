using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Events;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Features.Drivers;
using AegisDrive.Api.Features.Fleet;
using AegisDrive.Api.Features.Monitoring;
using AegisDrive.Api.Features.SafetyEvents;
// using AegisDrive.Api.Hubs; // SignalR - Commented out
using AegisDrive.Api.Shared;
using Amazon.SQS;
using Amazon.SQS.Model;
using MediatR;
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

    public SafetyEventSqsConsumer(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> sqsSettings,
        ILogger<SafetyEventSqsConsumer> logger,
        IConnectionMultiplexer redisMux,
        IServiceProvider serviceProvider)
    {
        _sqsClient = sqsClient;
        // Connect to the NORMAL queue (High/Medium events)
        _queueUrl = sqsSettings.Value.DrowsinessEventsQueueUrl;
        _logger = logger;
        _redisMux = redisMux;
        _serviceProvider = serviceProvider;
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
                    MaxNumberOfMessages = 10, // Process batches for efficiency
                    WaitTimeSeconds = 20,     // Long polling
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var sqsMessage in response.Messages ?? new List<Message>())
                {
                    // Process message
                    if (await ProcessMessageAsync(sqsMessage, stoppingToken))
                    {
                        // Delete on success
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

            _logger.LogInformation("📩 Received  Event: MessageId={MessageId}", sqsMessage.MessageId);

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
            // var hubContext = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<FleetHub>>(); // SignalR - Commented out
            var redis = _redisMux.GetDatabase();

            if (await safetyRepository.AnyAsync(sf => sf.Id == message.EventId, token))
            {
                return true;
            }

            // 3. Enrichment (Vehicle/Driver)
            var vehicleResult = await sender.Send(new GetVehicle.Query(message.VehicleId), token);
            if (vehicleResult.IsFailure || vehicleResult.Value.CurrentDriverId == null)
            {
                _logger.LogWarning("Skipping event {Id}: Vehicle/Driver not found.", message.EventId);
                return true; // Mark handled to remove from queue
            }
            var vehicleData = vehicleResult.Value;
            var driverId = vehicleData.CurrentDriverId.Value;

            var driverResult = await sender.Send(new GetDriverProfile.Query(driverId), token);
            var driverProfile = driverResult.Value;

            // 4. Get Location (Redis)
            var liveState = await sender.Send(new GetVehicleLiveState.Query(message.VehicleId), token);
            double speed = liveState.Value?.LiveLocation?.SpeedKmh ?? 0;
            string mapLink = liveState.Value?.LiveLocation != null
                ? GpsLinkUtility.GenerateMapsLink(liveState.Value.LiveLocation.Latitude, liveState.Value.LiveLocation.Longitude)
                : "Location Unavailable";

            // 5. Determine Alert Level & State
            Enum.TryParse<AlertLevel>(message.AlertLevel, true, out var alertLevel);
            Enum.TryParse<DriverState>(message.DriverState, true, out var driverState);
            DateTime eventTimestamp = message.GetParsedTimestamp();

            // 6. Save Event (Reuse the CreateCritical logic)
            // Note: For MEDIUM events, we might want to skip saving the Road Image to save space,
            // but keeping it simple for now and saving everything.
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

            // 7. Update Driver Safety Score
            await sender.Send(new DeductDriverSafteyScore.Command(driverId, alertLevel), token);

 

            // 9. Handle Notifications based on Level
            if (alertLevel == AlertLevel.HIGH)
            {
                // --- HIGH ALERT: Notify Company Only ---
                // Rate Limit: 1 minute (Don't spam manager for every distraction)
                string cooldownKey = $"email_high_cooldown:{driverId}";
                bool canSend = await redis.StringSetAsync(cooldownKey, "1", TimeSpan.FromMinutes(1), When.NotExists);

                if (canSend && driverProfile?.DriverCompany != null && !string.IsNullOrEmpty(driverProfile.DriverCompany.RepresentativeEmail))
                {
                    // Use High Alert Email Template (No Images in Email body, link to dashboard)
                    await notificationService.SendHighAlertAsync(
                        driverProfile.DriverCompany.RepresentativeEmail,
                        driverProfile.FullName,
                        vehicleData.PlateNumber,
                        message.Message,
                        message.DriverState, // e.g. "DISTRACTED"
                        message.EventId,
                        message.DeviceId
                    );
                    _logger.LogInformation("📧 High Alert sent to Company.");
                }

                // SignalR: Push Orange Alert to Dashboard (Commented out)
                /*
                if (vehicleData.CompanyId.HasValue)
                {
                    await hubContext.Clients.Group($"Company_{vehicleData.CompanyId}")
                        .SendAsync("HighAlert", new { 
                            VehicleId = vehicleData.VehicleId,
                            Message = message.Message,
                            DriverImage = driverImageUrl,
                            Timestamp = eventTimestamp
                        }, token);
                }
                */
            }
            else if (alertLevel == AlertLevel.MEDIUM)
            {
                // --- MEDIUM ALERT: Dashboard Update Only ---
                // No Email. Just update the map status to "Fatigue Detected".
                /*
                if (vehicleData.CompanyId.HasValue)
                {
                    await hubContext.Clients.Group($"Company_{vehicleData.CompanyId}")
                        .SendAsync("DriverStatusUpdate", new { 
                            VehicleId = vehicleData.VehicleId,
                            Status = "Fatigue Detected", 
                            AlertLevel = "MEDIUM",
                            DriverImage = driverImageUrl
                        }, token);
                }
                */
            }

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError("❌ JSON Parse Error: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error processing safety event");
            return false;
        }
    }
}