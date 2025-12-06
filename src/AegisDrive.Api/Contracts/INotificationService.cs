namespace AegisDrive.Api.Contracts;

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body);
    
    Task SendCriticalAlertAsync(
        string to,
        string driverName,
        string vehiclePlate,
        string message,
        string eventType,
        string mapLink,
        string driverImgUrl,
        string roadImgUrl,
        Guid eventId,
        double speed,
        string driverProfilePicUrl,                
        string deviceId             
    );
}
