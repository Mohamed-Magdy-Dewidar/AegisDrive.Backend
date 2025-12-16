using AegisDrive.Api.Contracts.Analytics;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Analytics;

public static class GetDashboardStats
{
    public record Query(int? DriverId, int? CompanyId) : IRequest<Result<GetDashboardStatsResponse>>;

    
    

    private record EventSummary(AlertLevel AlertLevel, DriverState DriverState);

    internal sealed class Handler : IRequestHandler<Query, Result<GetDashboardStatsResponse>>
    {
        private readonly AppDbContext _context;
        private readonly IDatabase _redis;

        public Handler(AppDbContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        public async Task<Result<GetDashboardStatsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string cacheKey = request.DriverId.HasValue
                ? $"dashboard:stats:driver:{request.DriverId}"
                : $"dashboard:stats:company:{request.CompanyId}";

            var cachedData = await _redis.StringGetAsync(cacheKey);
            if (!cachedData.IsNull)
            {
                var cachedResponse = JsonSerializer.Deserialize<GetDashboardStatsResponse>(cachedData!);
                if (cachedResponse != null) return Result<GetDashboardStatsResponse>.Success(cachedResponse);
            }

            GetDashboardStatsResponse? result = null;
            var fromDate = DateTime.UtcNow.Date.AddDays(-7);

            if (request.DriverId.HasValue)
            {
                var driverStats = await _context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Id == request.DriverId)
                    .Select(d => new { d.SafetyScore })
                    .FirstOrDefaultAsync(cancellationToken);

                if (driverStats == null)
                    return Result<GetDashboardStatsResponse>.Failure<GetDashboardStatsResponse>(new Error("Driver.NotFound", "Profile not found"));

                // Fetch Events
                var events = await _context.SafetyEvents
                    .AsNoTracking()
                    .Where(e => e.DriverId == request.DriverId && e.Timestamp >= fromDate)
                    .Select(e => new EventSummary(e.AlertLevel, e.DriverState)) // Map to concrete type
                    .ToListAsync(cancellationToken);

                result = BuildResponse(
                    driverStats.SafetyScore,
                    events,
                    totalVehicles: 1,
                    activeVehicles: 1
                );
            }
            // --- SCENARIO 2: COMPANY MANAGER ---
            else if (request.CompanyId.HasValue)
            {
                // 1. Avg Score
                var avgScore = await _context.Drivers
                    .Where(d => d.CompanyId == request.CompanyId)
                    .AverageAsync(d => (double?)d.SafetyScore, cancellationToken) ?? 100.0;

                // 2. Fetch Events (Fixing the CompanyId Access)
                var events = await _context.SafetyEvents
                    .AsNoTracking()
                    .Where(e => e.Driver != null && e.Driver.CompanyId == request.CompanyId && e.Timestamp >= fromDate)
                    .Select(e => new EventSummary(e.AlertLevel, e.DriverState)) // Map to concrete type
                    .ToListAsync(cancellationToken);

                // 3. Vehicle Stats
                var vehicleCounts = await _context.Vehicles
                    .AsNoTracking()
                    .Where(v => v.CompanyId == request.CompanyId)
                    .GroupBy(v => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Active = g.Count(v => v.Status == VehicleStatus.Active)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                int totalV = vehicleCounts?.Total ?? 0;
                int activeV = vehicleCounts?.Active ?? 0;

                result = BuildResponse(avgScore, events, totalV, activeV);
            }

            if (result == null)
                return Result<GetDashboardStatsResponse>.Failure<GetDashboardStatsResponse>(new Error("Auth.Invalid", "Invalid Dashboard Context"));

            await _redis.StringSetAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                TimeSpan.FromSeconds(60));

            return Result<GetDashboardStatsResponse>.Success(result);
        }


        private static GetDashboardStatsResponse BuildResponse(double score, List<EventSummary> events, int totalVehicles, int activeVehicles)
        {
            // 1. Safety Level Calculation
            string level = score switch
            {
                >= 90 => "Excellent",
                >= 75 => "Good",
                >= 60 => "At Risk",
                _ => "Critical"
            };

            // 2. Event Aggregation
            int critical = 0, high = 0, medium = 0;
            int drowsy = 0, distracted = 0;

            foreach (var e in events)
            {
                // A. Level Counts
                if (e.AlertLevel == AlertLevel.CRITICAL) critical++;
                else if (e.AlertLevel == AlertLevel.HIGH) high++;
                else if (e.AlertLevel == AlertLevel.MEDIUM) medium++;

                // B. Category Counts (Strongly Typed Switch)
                switch (e.DriverState)
                {
                    // --- Drowsiness Group ---
                    case DriverState.DROWSY:
                    case DriverState.YAWNING:
                    case DriverState.DROWSY_YAWNING:
                        drowsy++;
                        break;

                    // --- Distraction Group ---
                    case DriverState.DISTRACTED:
                    case DriverState.NO_FACE_DETECTED: // Often implies looking away/down
                        distracted++;
                        break;

                    // --- Combined Group (Counts for both) ---
                    case DriverState.DROWSY_DISTRACTED:
                        drowsy++;
                        distracted++;
                        break;

                    // --- Ignore Safe/Other ---
                    case DriverState.ALERT:
                    default:
                        break;
                }
            }

            return new GetDashboardStatsResponse(
                Math.Round(score, 1),
                level,
                events.Count,
                critical,
                high,
                medium,
                drowsy,
                distracted,
                totalVehicles,
                activeVehicles,
                totalVehicles - activeVehicles,
                DateTime.UtcNow
            );
        }


    }
}