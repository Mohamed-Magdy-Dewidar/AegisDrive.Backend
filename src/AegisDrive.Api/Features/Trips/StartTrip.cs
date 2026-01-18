using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Features.Vehicles;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Trips;

public static class StartTrip
{
    public record Command(
        int VehicleId,
        string DestinationText,
        decimal DestinationLat,
        decimal DestinationLng,        
        int DriverId
    ) : ICommand<Result<TripResponse>>;

    public record TripResponse(
        Guid TripId,
        object Geometry,
        double Distance,
        double Duration,
        string DestinationText
    );

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DriverId).NotEmpty().GreaterThan(0);
        }
    }

    public record OsrmResponse(List<OsrmRoute> Routes);
    public record OsrmRoute(double Distance, double Duration, object Geometry);

    internal sealed class Handler : IRequestHandler<Command, Result<TripResponse>>
    {
        private readonly IGenericRepository<Trip, Guid> _tripRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDatabase _redis;
        private readonly ISender _sender;
        private readonly IValidator<Command> _Validator;

        public Handler(
            IGenericRepository<Trip, Guid> tripRepository,
            IHttpClientFactory httpClientFactory,
            IConnectionMultiplexer connectionMultiplexer,
            ISender sender,
            IValidator<Command> Validator)
        {
            _tripRepository = tripRepository;
            _httpClientFactory = httpClientFactory;
            _redis = connectionMultiplexer.GetDatabase();
            _sender = sender;
            _Validator = Validator;
        }

        public async Task<Result<TripResponse>> Handle(Command request, CancellationToken cancellationToken)
        {


            var validationResult = await _Validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Result.Failure<TripResponse>(new Error("TripStart.Validation", validationResult.ToString()));
            }

            // 1. Check if vehicle is already in an active trip
            var activeTripKey = $"active_trip:vehicle:{request.VehicleId}";
            var existingTrip = await _redis.StringGetAsync(activeTripKey);

            if (!existingTrip.IsNull)
            {
                return Result.Failure<TripResponse>(new Error("Trip.Conflict", "Vehicle is already assigned to an active trip."));
            }

            var GetVehicleIdByDriverIdData = await _sender.Send(new GetVehicleIdByDriverId.Query(DriverId:  request.DriverId));
            var vehicleId = GetVehicleIdByDriverIdData.Value.VehicleId;


            // 2. Get Starting GPS from the specialized query
            var gpsDataResult = await _sender.Send(new GetVehicleLatestCoordinates.Query(vehicleId), cancellationToken);

            if (!gpsDataResult.IsSuccess)
            {
                return Result.Failure<TripResponse>(new Error("Trip.GpsNotFound", "Could not retrieve vehicle GPS data."));
            }

            var startLat = gpsDataResult.Value.Latitude;
            var startLng = gpsDataResult.Value.Longitude;

            // 3. Call OSRM API to get the Route
            var osrmUrl = $"http://router.project-osrm.org/route/v1/driving/" +
                          $"{startLng},{startLat};{request.DestinationLng},{request.DestinationLat}" +
                          $"?overview=full&geometries=geojson";

            using var client = _httpClientFactory.CreateClient();
            var osrmResponse = await client.GetFromJsonAsync<OsrmResponse>(osrmUrl, cancellationToken);

            if (osrmResponse?.Routes == null || osrmResponse.Routes.Count == 0)
            {
                return Result.Failure<TripResponse>(new Error("Trip.RouteError", "Could not calculate a valid route to the destination."));
            }

            var route = osrmResponse.Routes[0];

            // 4. Create the Trip Entity
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                DriverId = request.DriverId,
                VehicleId = vehicleId,
                Status = TripStatus.Active,
                StartLat = (decimal)startLat,
                StartLng = (decimal)startLng,
                StartTime = DateTime.UtcNow,
                DestinationText = request.DestinationText,
                DestinationLat = request.DestinationLat,
                DestinationLng = request.DestinationLng,
                EstimatedDistanceMeters = route.Distance,
                EstimatedDurationSeconds = route.Duration,
                RouteGeometryJson = JsonSerializer.Serialize(route.Geometry)
            };

            // 5. Persist and Cache
            await _tripRepository.AddAsync(trip);

            // Set Redis cache for 12 hours to mark vehicle as busy
            await _redis.StringSetAsync(activeTripKey, trip.Id.ToString(), TimeSpan.FromHours(12));

            return Result.Success(new TripResponse(
                trip.Id,
                route.Geometry,
                trip.EstimatedDistanceMeters,
                trip.EstimatedDurationSeconds,
                trip.DestinationText
            ));
        }
    }
}