namespace AegisDrive.Api.Contracts.Analytics;


public record GetDashboardStatsResponse(
        double SafetyScore,
        string SafetyLevel,
        int TotalEventsThisWeek,
        int CriticalCount,
        int HighCount,
        int MediumCount,
        int DrowsinessEvents,
        int DistractionEvents,
        int TotalVehicles,
        int ActiveVehicles,
        int InactiveVehicles,
        DateTime LastUpdated
    );
