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
        Guid eventId,
        double speed,
        string deviceId
    );

    public  Task SendHighAlertAsync(string to, string driverName, string vehiclePlate, string message, string eventType, Guid eventId, string deviceId);

}
