using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Features.Drivers;
using AegisDrive.Api.Features.Vehicles;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using StackExchange.Redis;
using static AegisDrive.Api.Features.Trips.StartTrip;

namespace AegisDrive.Api.Features.Trips;

public static class EndTrip
{
    public record Command(Guid TripId) : ICommand<Result<TripSummaryResponse>>;

    public record TripSummaryResponse(
        double TripScore,
        double NewGlobalDriverScore,
        int DrowsinessEvents,
        int DistractionEvents,
        TimeSpan Duration
    );

    internal sealed class Handler : IRequestHandler<Command, Result<TripSummaryResponse>>
    {
        private readonly IGenericRepository<Trip, Guid> _tripRepository;
        private readonly ISender _sender;
        private readonly IDatabase _redis;

        public Handler(
            IGenericRepository<Trip, Guid> tripRepository,
            ISender sender,
            IConnectionMultiplexer redisMux)
        {
            _tripRepository = tripRepository;
            _sender = sender;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<TripSummaryResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            // 1. Get the Trip (using allowed repository)
            var trip = await _tripRepository.GetByIdAsync(request.TripId);
            
            if (trip == null) return Result<TripSummaryResponse>.Failure<TripSummaryResponse>(new Error("Trip.NotFound", "Trip not found."));
            
            if (trip.Status == TripStatus.Completed) 
                return Result<TripSummaryResponse>.Failure<TripSummaryResponse>(new Error("Trip.Finalized", "Trip is already completed."));

            var endTime = DateTime.UtcNow;

            var gpsDataResult = await _sender.Send(new GetVehicleLatestCoordinates.Query(trip.VehicleId), cancellationToken);

            if (gpsDataResult.IsSuccess)
            {
                var gpsData = gpsDataResult.Value;
                trip.EndLat = (decimal)gpsData.Latitude;
                trip.EndLng = (decimal)gpsData.Longitude;    
            }



            var metricsResult = await _sender.Send(new GetTripEventMetrics.Query(trip.VehicleId, trip.StartTime, endTime), cancellationToken);

            if (!metricsResult.IsSuccess) 
                return Result<TripSummaryResponse>.Failure<TripSummaryResponse>(metricsResult.Error);
            
            var m = metricsResult.Value;

            // 3. Calculate Trip Safety Score
            // Formula: 100 - (Critical*10 + High*50 + Medium*3)
            double tripScore = 100 - (m.CriticalCount * 10) - (m.HighCount * 5) - (m.MediumCount * 3);
            trip.TripSafetyScore = Math.Max(0, tripScore);
            trip.Status = TripStatus.Completed;
            trip.EndTime = endTime;



            // 4. Update the Database for the Trip         
            string[] includes = 
                [ nameof(trip.TripSafetyScore), nameof(trip.Status) , nameof(trip.EndTime) , nameof(trip.EndLat) , nameof(trip.EndLng)];
            
            _tripRepository.SaveInclude(trip , includes);

            // 5. Update Global Driver Score
            // We delegate this to a separate feature to avoid injecting the Driver Repo here
            var updateDriverResult = await _sender.Send(new UpdateDriverGlobalScore.Command(trip.DriverId, trip.TripSafetyScore), cancellationToken);

            // 6. Cleanup Redis
            await _redis.KeyDeleteAsync($"active_trip:vehicle:{trip.VehicleId}");

            return Result<TripSummaryResponse>.Success(new TripSummaryResponse(
                trip.TripSafetyScore,
                updateDriverResult.IsSuccess ? updateDriverResult.Value : 0,
                m.DrowsinessCount,
                m.DistractionCount,
                endTime - trip.StartTime
            ));
        }
    }
}