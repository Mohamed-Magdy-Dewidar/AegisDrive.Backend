using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Events;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Features.Drivers;
using AegisDrive.Api.Features.Fleet;
using AegisDrive.Api.Features.Monitoring;
using AegisDrive.Api.Features.SafetyEvents;
using AegisDrive.Api.Shared;
using Amazon.SQS;
using Amazon.SQS.Model;
using MediatR;
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
    
    public CriticalEventSqsConsumer(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> SqsSettings,
        IConfiguration configuration,
        ILogger<CriticalEventSqsConsumer> logger,
        IConnectionMultiplexer redisMux,
        IServiceProvider serviceProvider
        )
    {
        _sqsClient = sqsClient;
        _queueUrl = SqsSettings.Value.DrowsinessCriticalEventsQueueUrl;
        _logger = logger;
        _redisMux = redisMux;
        _serviceProvider = serviceProvider;
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
                    MaxNumberOfMessages = 1, // Process one critical event at a time
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
            var message = JsonSerializer.Deserialize<CriticalEventMessage>(
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
            
            // 6. Send Notifications (With Redis Rate Limiting)
            string cooldownKey = $"email_cooldown:{vehicleData.CurrentDriverId}";

            // Atomic check and set with 30-second cooldown
            bool canSendEmail = await redis.StringSetAsync(
                cooldownKey,
                "sent",
                TimeSpan.FromSeconds(30),
                When.NotExists
            );

            if (!canSendEmail)
            {
                _logger.LogInformation("⏳ Email cooldown active for driver {DriverId}, skipping notifications",
                    vehicleData.CurrentDriverId);
                return true; // Event saved, just skipping notifications
            }
            var SafetyEventResultData = SafetyEventSaveResult.Value;
            string driverImageUrl = _fileStorageService.GetPresignedUrl(SafetyEventResultData.DriverImageKey ?? "");
            string roadImageUrl = _fileStorageService.GetPresignedUrl(SafetyEventResultData.RoadImageKey ?? "");
            string? profilePicUrl = string.IsNullOrEmpty(driverProfile?.PictureUrl) ? null : driverProfile.PictureUrl;


            // Send to Company Representative
            if (driverProfile?.DriverCompany != null && !string.IsNullOrEmpty(driverProfile?.DriverCompany?.RepresentativeEmail))
            {
                await notificationService.SendCriticalAlertAsync(
                    driverProfile.DriverCompany.RepresentativeEmail,
                    driverProfile.FullName,
                    vehicleData.PlateNumber,
                    message.Message,
                    message.DriverState,
                    mapLink,
                    driverImageUrl,
                    roadImageUrl,
                    message.EventId,
                    speed,
                    profilePicUrl,
                    message.DeviceId
                );
                _logger.LogInformation("📧 Alert sent to Company: {Email}", driverProfile?.DriverCompany.RepresentativeEmail);
            }

            // Send to Family Members
            if (driverProfile?.DriverFamilyMembers != null)
            {
                foreach (var family in driverProfile.DriverFamilyMembers.Where(f => f.NotifyOnCritical))
                {
                    if (!string.IsNullOrEmpty(family.Email))
                    {
                        await notificationService.SendCriticalAlertAsync(
                            family.Email,
                            driverProfile.FullName,
                            vehicleData.PlateNumber,
                            message.Message,
                            message.DriverState,
                            mapLink,
                            driverImageUrl,
                            roadImageUrl,
                            message.EventId,
                            speed,
                            profilePicUrl,
                            message.DeviceId
                        );

                        _logger.LogInformation("📧 Alert sent to Family: {Name} ({Email})",family.FullName, family.Email);
                    }
                }
            }

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
}