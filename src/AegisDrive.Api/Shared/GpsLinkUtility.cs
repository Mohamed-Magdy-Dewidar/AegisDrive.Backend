using System.Globalization;
namespace AegisDrive.Api.Shared;




public static class GpsLinkUtility
{
    private const string GoogleMapsBaseUrl = "https://www.google.com/maps?q=";

    /// <summary>
    /// Generates a Google Maps URL for a given latitude and longitude.
    /// </summary>
    /// <param name="latitude">The latitude coordinate.</param>
    /// <param name="longitude">The longitude coordinate.</param>
    /// <returns>A complete, clickable Google Maps URL.</returns>
    public static string GenerateMapsLink(double latitude, double longitude)
    {
        // Use InvariantCulture to ensure the decimal separator is always a period (.),
        // which is required for URLs, regardless of the user's regional settings.
        var latString = latitude.ToString(CultureInfo.InvariantCulture);
        var longString = longitude.ToString(CultureInfo.InvariantCulture);

        // The URL format is: https://www.google.com/maps?q={latitude},{longitude}
        return $"{GoogleMapsBaseUrl}{latString},{longString}";
    }
}