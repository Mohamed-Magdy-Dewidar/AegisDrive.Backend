using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Trips;

public static class GetTripEventMetrics
{
    public record Query(int VehicleId , Guid tripId ) : IRequest<Result<MetricsResponse>>;

    public record MetricsResponse(
        int CriticalCount,
        int HighCount,
        int MediumCount,
        int DrowsinessCount,
        int DistractionCount);

    internal sealed class Handler(IGenericRepository<SafetyEvent, Guid> safetyRepository)
        : IRequestHandler<Query, Result<MetricsResponse>>
    {
        public async Task<Result<MetricsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var events = await safetyRepository.GetAll()
                .Where(e => e.VehicleId == request.VehicleId && e.TripId == request.tripId)
                .Select(e => new { e.AlertLevel, e.DriverState })
                .ToListAsync(cancellationToken);

            int drowsinessCount = 0;
            int distractionCount = 0;


            foreach (var e in events)
            {
                switch (e.DriverState)
                {
                    case DriverState.DROWSY:
                    case DriverState.YAWNING:
                    case DriverState.DROWSY_YAWNING:
                        drowsinessCount++;
                        break;

                    case DriverState.DISTRACTED:
                    case DriverState.NO_FACE_DETECTED:
                        distractionCount++;
                        break;

                    case DriverState.DROWSY_DISTRACTED:
                        drowsinessCount++;
                        distractionCount++;
                        break;

                    default:
                        break;
                }
            }

            return Result<MetricsResponse>.Success(new MetricsResponse(
                events.Count(e => e.AlertLevel == AlertLevel.CRITICAL),
                events.Count(e => e.AlertLevel == AlertLevel.HIGH),
                events.Count(e => e.AlertLevel == AlertLevel.MEDIUM),
                drowsinessCount,
                distractionCount
            ));
        }
    }
}