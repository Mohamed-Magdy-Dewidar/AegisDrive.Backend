namespace AegisDrive.Api.Shared;

/// <summary>
/// Centralizes S3 folder structure logic.
/// Ensures consistent naming for Companies vs. Individuals.
/// </summary>
public static class FilePaths
{
    // --- 1. Base Folder Names (Constants) ---
    public const string FleetsRoot = "fleets";
    public const string IndividualsRoot = "individuals";
    public const string EventsFolder = "events";
    public const string ProfilesFolder = "profiles";



    // --- 2. Path Generators (The Smart Part) ---

    /// <summary>
    /// Generates path for a Safety Event (Context Aware)
    /// Output: "fleets/5/events/2025/11/26" OR "individuals/99/events/2025/11/26"
    /// </summary>
    public static string GetEventPath(int? companyId, int driverId, DateTime eventDate)
    {
        string root = companyId.HasValue
            ? $"{FleetsRoot}/{companyId.Value}"
            : $"{IndividualsRoot}/{driverId}";

        // Organize by Date to prevent folders from having 1,000,000 files (S3 performance best practice)
        return $"{root}/{EventsFolder}/{eventDate:yyyy/MM/dd}";
    }




    /// <summary>
    /// Generates path for a Driver's Profile Picture
    /// Output: "fleets/5/drivers/2/profile" OR "individuals/12/profile"
    /// </summary>
    public static string GetDriverProfilePath(int? companyId, int driverId)
    {
        string root = companyId.HasValue
            ? $"{FleetsRoot}/{companyId.Value}/drivers/{driverId}"
            : $"{IndividualsRoot}/{driverId}";

        return $"{root}/{ProfilesFolder}";
    }
}