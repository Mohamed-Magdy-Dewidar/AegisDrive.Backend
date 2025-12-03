namespace AegisDrive.Api.Entities.Enums;


public enum AlertLevel
{
    NONE,    // Everything normal
    LOW,     // Minor concern
    MEDIUM,  // Driver issue OR road hazard
    HIGH,    // Driver issue AND road hazard
    CRITICAL // Imminent danger
}


