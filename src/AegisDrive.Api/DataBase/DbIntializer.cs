
using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Identity;
using AegisDrive.Api.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace AegisDrive.Api.DataBase;



public class DbIntializer : IDbIntializer
{
    private readonly AppDbContext _context;
    private readonly ILogger<DbIntializer> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;



    public DbIntializer(AppDbContext context,ILogger<DbIntializer> logger,UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
        _roleManager = roleManager;
    }


    public async Task MigrateAsync()
    {

        // Wait for the database to be connectable
        await WaitForDatabaseAsync();

        var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            _logger.LogInformation("Applying pending migrations for UserProfileDbContext...");
            await _context.Database.MigrateAsync();
        }

    }


    public async Task IdentitySeedDataAsync()
    {
        try
        {
            _logger.LogInformation("Seeding Identity Data...");

            // A. Seed Roles
            string[] roles = { AuthConstants.Roles.Manager, AuthConstants.Roles.Individual };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                    _logger.LogInformation($"Created Role: {role}");
                }
            }

            // B. Ensure a Default Company Exists (For the Manager)
            // We check if any company exists (from SeedDataAsync). If not, we create a fallback.
            Company? defaultCompany = await _context.Companies.FirstOrDefaultAsync();
            if (defaultCompany == null)
            {
                defaultCompany = new Company
                {
                    Name = "Aegis Logistics Default",
                };
                _context.Companies.Add(defaultCompany);
                await _context.SaveChangesAsync();
            }

            // C. Seed Users

            // 1. Manager User (Must belong to a Company)
            var managerEmail = "manager@aegis.com";
            if (await _userManager.FindByEmailAsync(managerEmail) == null)
            {
                var manager = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sarah Connor",
                    EmailConfirmed = true,
                    CompanyId = defaultCompany.Id // Linked to Company
                };

                var result = await _userManager.CreateAsync(manager, "Password123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(manager, AuthConstants.Roles.Manager);
                    _logger.LogInformation("Seeded Manager User.");
                }
            }

            // 2. Individual/Driver User (No Company)
            var driverEmail = "driver@gmail.com";
            if (await _userManager.FindByEmailAsync(driverEmail) == null)
            {
                var driver = new ApplicationUser
                {
                    UserName = driverEmail,
                    Email = driverEmail,
                    FullName = "Max Rockatansky",
                    EmailConfirmed = true,
                    CompanyId = null // Individual
                };

                var result = await _userManager.CreateAsync(driver, "Password123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(driver, AuthConstants.Roles.Individual);
                    _logger.LogInformation("Seeded Individual User.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding identity data.");
            throw;
        }
    }
        

    public async Task SeedDataAsync()
    {

        


        // 1. COMPANIES
        if (!await _context.Companies.AnyAsync())
        {
            _logger.LogInformation("Seeding Companies...");
            var companiesData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/Companies.json");
            var companies = JsonSerializer.Deserialize<List<Company>>(companiesData);

            if (companies != null && companies.Any())
            {
                _context.Companies.AddRange(companies);
                await _context.SaveChangesAsync();
            }
        }

        // 2. DRIVERS (Depends on Companies)
        if (!await _context.Drivers.AnyAsync())
        {
            _logger.LogInformation("Seeding Drivers...");
            var driversData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/Drivers.json");
            var drivers = JsonSerializer.Deserialize<List<Driver>>(driversData);

            if (drivers != null && drivers.Any())
            {
                _context.Drivers.AddRange(drivers);
                await _context.SaveChangesAsync();
            }
        }

        // 3. VEHICLES (Depends on Companies & Drivers for CurrentDriverId)
        if (!await _context.Vehicles.AnyAsync())
        {
            _logger.LogInformation("Seeding Vehicles...");
            var vehiclesData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/Vehicles.json");
            var vehicles = JsonSerializer.Deserialize<List<Vehicle>>(vehiclesData);

            if (vehicles != null && vehicles.Any())
            {
                _context.Vehicles.AddRange(vehicles);
                await _context.SaveChangesAsync();
            }
        }

        // 4. DEVICES (Depends on Vehicles)
        if (!await _context.Devices.AnyAsync())
        {
            _logger.LogInformation("Seeding Devices...");
            var devicesData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/Devices.json");
            var devices = JsonSerializer.Deserialize<List<Device>>(devicesData);

            if (devices != null && devices.Any())
            {
                _context.Devices.AddRange(devices);
                await _context.SaveChangesAsync();
            }
        }

        // 5. VEHICLE ASSIGNMENTS (Depends on Drivers & Vehicles)
        if (!await _context.VehicleAssignments.AnyAsync())
        {
            _logger.LogInformation("Seeding Vehicle Assignments...");
            var assignmentsData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/Vehicle_Assignments.json");
            var assignments = JsonSerializer.Deserialize<List<VehicleAssignment>>(assignmentsData);

            if (assignments != null && assignments.Any())
            {
                _context.VehicleAssignments.AddRange(assignments);
                await _context.SaveChangesAsync();
            }
        }

        // 6. SAFETY EVENTS (Depends on Drivers, Vehicles, Devices)
        if (!await _context.SafetyEvents.AnyAsync())
        {
            _logger.LogInformation("Seeding Safety Events...");
            var eventsData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/SafetyEvents.json");
            var events = JsonSerializer.Deserialize<List<SafetyEvent>>(eventsData);

            if (events != null && events.Any())
            {
                _context.SafetyEvents.AddRange(events);
                await _context.SaveChangesAsync();
            }
        }

        // 7. Telemetry EVENTS (Depends on Vehicles, Devices)
        if (!await _context.TelemetryEvents.AnyAsync())
        {

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            // <--- Critical for "IgnitionOn" -> Enum.IgnitionOn
            // to Map strings to Enum

            _logger.LogInformation("Seeding TelemetryEvents Events...");
            var eventsData = await File.ReadAllTextAsync(@"DataBase/DataSeed/RawData/TelemetryData.json");
            var events = JsonSerializer.Deserialize<List<TelemetryEvent>>(eventsData ,options);

            if (events != null && events.Any())
            {
                _context.TelemetryEvents.AddRange(events);
                await _context.SaveChangesAsync();
            }
        }
    }



    private async Task WaitForDatabaseAsync()
    {
        const int MaxRetries = 20;
        const int DelaySeconds = 10;
        for (int i = 0; i < MaxRetries; i++)
        {
            try
            {
                // Try to connect - this will work even if database doesn't exist
                await _context.Database.EnsureCreatedAsync();
                _logger.LogInformation("✅ Database ensured!");
                break;
            }
            catch (Exception ex) when (i < MaxRetries - 1)
            {
                _logger.LogWarning($"Database creation attempt {i + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(DelaySeconds);
            }
        }
    }


}