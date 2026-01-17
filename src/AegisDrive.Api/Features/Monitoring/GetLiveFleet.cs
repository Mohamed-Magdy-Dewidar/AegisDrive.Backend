using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles; 
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.Pagination;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;  

namespace AegisDrive.Api.Features.Monitoring;

public static class GetLiveFleet
{
    // 1. Query
    public record Query(
        int? CompanyId,
        int? DriverId,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<PagedResult<FleetVehicleLiveStateResponse>>>;

    // 2. Handler
    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<FleetVehicleLiveStateResponse>>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IDatabase _redis;

        public Handler(
            IGenericRepository<Vehicle, int> vehicleRepository,
            IConnectionMultiplexer redisMux)
        {
            _vehicleRepository = vehicleRepository;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<PagedResult<FleetVehicleLiveStateResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            // 1. QUERY DATABASE FOR IDs
            var query = _vehicleRepository.GetAll().AsNoTracking();

            if (request.CompanyId.HasValue)
            {
                query = query.Where(v => v.CompanyId == request.CompanyId.Value);
            }
            else
            {
                query = query.Where(v => request.DriverId.HasValue && v.CurrentDriverId == request.DriverId.Value);
            }

                var pagedIds = await query
                    .OrderByDescending(v => v.CreatedOnUtc)
                    .Select(v => new { v.Id })
                    .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            if (!pagedIds.Items.Any())
            {
                return Result.Success(new PagedResult<FleetVehicleLiveStateResponse>(
                    new List<FleetVehicleLiveStateResponse>(),
                    pagedIds.Pagination.TotalItems,
                    request.Page,
                    request.PageSize));
            }

            // 2. BATCH FETCH FROM REDIS (Pipelined Hash Get)
            // We use a Batch to send 50 HGETALL commands in 1 network roundtrip.
            var batch = _redis.CreateBatch();
            var tasks = new List<Task<HashEntry[]>>();

            foreach (var item in pagedIds.Items)
            {
                string key = $"vehicle:{item.Id}:live";
                tasks.Add(batch.HashGetAllAsync(key));
            }

            batch.Execute(); // Send all commands
            var redisResults = await Task.WhenAll(tasks); // Wait for all replies

            var finalResults = new List<FleetVehicleLiveStateResponse>();
            var missingIds = new List<int>();
            var itemsList = pagedIds.Items.ToList();

            // 3. PROCESS RESULTS
            for (int i = 0; i < redisResults.Length; i++)
            {
                var hashEntries = redisResults[i];
                var vehicleId = itemsList[i].Id;

                // Check if we got data AND if PlateNumber exists (Validation for "Warm" cache)
                var dict = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

                if (hashEntries.Length > 0 && dict.ContainsKey("PlateNumber"))
                {
                    // Map Hash -> DTO
                    finalResults.Add(new FleetVehicleLiveStateResponse(
                        vehicleId,
                        dict.GetValueOrDefault("PlateNumber", "Unknown"),
                        dict.GetValueOrDefault("Status", "Active"),
                        new FleetLiveLocationResponse(
                            double.Parse(dict.GetValueOrDefault("Latitude", "0")),
                            double.Parse(dict.GetValueOrDefault("Longitude", "0")),
                            double.Parse(dict.GetValueOrDefault("SpeedKmh", "0")),
                            DateTime.Parse(dict.GetValueOrDefault("LastUpdateUtc", DateTime.UtcNow.ToString("o")))
                        )
                    ));
                }
                else
                {
                    // Missing or incomplete data (e.g. only Lat/Lon from Ingest, but no Plate)
                    missingIds.Add(vehicleId);
                }
            }

            // 4. BATCH FETCH FROM DB (Fallback)
            if (missingIds.Any())
            {
                var dbVehicles = await _vehicleRepository.GetAll()
                    .AsNoTracking()
                    .Where(v => missingIds.Contains(v.Id))
                    .Select(v => new
                    {
                        v.Id,
                        v.PlateNumber,
                        v.Status,
                        LatestTelemetry = v.TelemetryEvents
                            .OrderByDescending(t => t.Timestamp)
                            .FirstOrDefault()
                    })
                    .ToListAsync(cancellationToken);

                foreach (var v in dbVehicles)
                {
                    var response = new FleetVehicleLiveStateResponse(
                        v.Id,
                        v.PlateNumber ?? "N/A",
                        v.Status.ToString(),
                        v.LatestTelemetry != null
                            ? new FleetLiveLocationResponse(v.LatestTelemetry.Latitude, v.LatestTelemetry.Longitude, v.LatestTelemetry.SpeedKmh, v.LatestTelemetry.Timestamp)
                            : new FleetLiveLocationResponse(0, 0, 0, DateTime.UtcNow)
                    );

                    finalResults.Add(response);

                    // Backfill Redis (Using HashSet)
                    var entries = new HashEntry[]
                    {
                        new HashEntry("PlateNumber", response.PlateNumber),
                        new HashEntry("Status", response.Status),
                        new HashEntry("Latitude", response?.LiveLocation?.Latitude),
                        new HashEntry("Longitude", response?.LiveLocation?.Longitude),
                        new HashEntry("SpeedKmh", response ?.LiveLocation ?.SpeedKmh),
                        new HashEntry("LastUpdateUtc", response?.LiveLocation?.LastUpdateUtc.ToString("o"))
                    };

                    // Fire-and-forget backfill
                    string key = $"vehicle:{v.Id}:live";
                    await _redis.HashSetAsync(key, entries);
                    await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(2));
                }
            }

            // 5. SORT & RETURN
            var sortedResults = finalResults.OrderBy(x => x.VehicleId).ToList();

            var result = new PagedResult<FleetVehicleLiveStateResponse>(
                sortedResults,
                pagedIds.Pagination.TotalItems,
                pagedIds.Pagination.Page,
                pagedIds.Pagination.PageSize);

            return Result.Success(result);
        }
    }
}