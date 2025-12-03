
using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace AegisDrive.Api.DataBase;



public class DbIntializer : IDbIntializer
{
    private readonly AppDbContext _context;
    private readonly ILogger<DbIntializer> _logger;






    public DbIntializer(AppDbContext context , ILogger<DbIntializer> logger)
    {
        _context = context;
        _logger = logger;
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