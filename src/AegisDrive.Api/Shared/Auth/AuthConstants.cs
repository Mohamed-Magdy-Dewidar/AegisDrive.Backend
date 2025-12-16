namespace AegisDrive.Api.Shared.Auth;


public static class AuthConstants
{
    // 1. Roles
    public static class Roles
    {
        public const string Admin = "Admin";         // System Admin
        public const string Manager = "Manager";     // Fleet Manager (Company)
        public const string Individual = "Individual"; // Solo Driver (Personal)
    }

    public static class AccountTypes
    {
        public static IReadOnlyList<string> AvailableAccountTypes = [AccountTypes.Company, AccountTypes.Individual];
        public const string Individual = "Individual"; // Solo Driver (Personal)
        public const string Company = "Company";       // Company Account
    }


    // 2. Custom Claims
    public static class Claims
    {
        public const string CompanyId = "company_id";
        public const string FullName = "full_name";
        public const string DriverId = "driver_id";
    }

    // 3. Policies (For [Authorize(Policy = ...)])
    public static class Policies
    {
        public const string CompanyOnly = "CompanyOnlyPolicy";
    }
}
