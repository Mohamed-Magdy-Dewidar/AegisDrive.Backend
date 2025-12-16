namespace AegisDrive.Api.Contracts.RealTime;



// 2. The Interface (The Actions)
public interface IFleetClient
{
    // The Dashboard Map will listen to this
    Task ReceiveVehicleUpdate(VehicleTelemetryUpdate update);

    // The Notification Bell/Popup will listen to this
    Task ReceiveCriticalAlert(CriticalAlertNotification alert);

    Task ReceiveHighLevelAlert(HighAlertNotification alert);


}