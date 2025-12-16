using AegisDrive.Api.Contracts;
using AegisDrive.Api.Shared.Email;
using AegisDrive.Infrastructure.Services.Notification.Templates;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AegisDrive.Api.Shared.Services;


public class SesNotificationService : INotificationService
{

    private readonly IAmazonSimpleEmailService _sesClient;
    private readonly ILogger<SesNotificationService> _logger;
    private readonly EmailSettings _SesEmailSettings;
    public SesNotificationService(IAmazonSimpleEmailService sesClient, ILogger<SesNotificationService> logger , IOptions<EmailSettings> settings)
    {
        _sesClient = sesClient;
        _logger = logger;
        _SesEmailSettings = settings.Value;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {

        
        // Uses the General Template
        var templateData = new
        {
            Subject = subject,
            Body = body
        };

        await SendTemplatedEmail(to, EmailTemplates.GeneralNotificationTemplateName, templateData);
    }

    public async Task SendHighAlertAsync(string to,string driverName,string vehiclePlate,string message,string eventType,Guid eventId ,string deviceId)
    {
        var templateData = new
        {
            DriverName = driverName,
            VehiclePlate = vehiclePlate,
            Message = message,
            EventType = eventType,
            EventId = eventId.ToString(),
            Timestamp = DateTime.UtcNow.ToString("g"),
            DeviceId = deviceId
        };

        // Use the new High Alert Template
        await SendTemplatedEmail(to, EmailTemplates.HighAlertTemplateName, templateData);
    }

    //public async Task SendCriticalAlertAsync(string to,string driverName,string vehiclePlate,string message,
    //    string eventType,string mapLink,string driverImgUrl,string roadImgUrl,Guid eventId,double speed,string driverProfilePicUrl ,  string deviceId)            
    //{
       
    //}


    private async Task SendTemplatedEmail(string to, string templateName, object data)
    {
        try
        {
            var sendRequest = new SendTemplatedEmailRequest
            {
                Source = _SesEmailSettings.SenderEmail,
                Destination = new Destination { ToAddresses = [to] },
                Template = templateName,
                TemplateData = JsonSerializer.Serialize(data) // SES requires JSON string
            };

            var response = await _sesClient.SendTemplatedEmailAsync(sendRequest);
            _logger.LogInformation("Email sent to {Recipient}. MsgId: {MsgId}", to, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", to);
            // Don't throw, we don't want to crash the message consumer for an email failure
        }
    }


    // Specialized method for Critical Alerts (Called by your CriticalEventMessageHandler)
    public async Task SendCriticalAlertAsync(
        string to,
        string driverName,
        string vehiclePlate,
        string message,
        string eventType,
        string mapLink,
        Guid eventId,
        double speed,
        string deviceId
    )
    {
        var templateData = new
        {
            DriverName = driverName,
            VehiclePlate = vehiclePlate,
            Message = message,
            EventType = eventType,
            MapLink = mapLink,
            EventId = eventId.ToString(),
            Speed = speed,
            Timestamp = DateTime.UtcNow.ToString("g"),
            DeviceId = deviceId
        };

        await SendTemplatedEmail(to, EmailTemplates.CriticalAlertTemplateName, templateData);
    }

    
}