using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Drivers;

public static class GetDriverProfile
{
    public record Query(int DriverId) : IRequest<Result<GetDriverProfileResponse>>;



    internal sealed class Handler : IRequestHandler<Query, Result<GetDriverProfileResponse>>
    {
        private readonly IGenericRepository<Driver , int> _driversRepository;
        private readonly IFileStorageService _fileStorageService;

        public Handler(IGenericRepository<Driver , int> driversRepository , IFileStorageService fileStorageService)
        {
            _driversRepository = driversRepository;
            _fileStorageService = fileStorageService;
        }

       public async Task<Result<GetDriverProfileResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var driverDto = await _driversRepository.GetAll()
                .Where(d => d.Id == request.DriverId)
                .Select(d => new GetDriverProfileResponse(
                    d.FullName,
                    d.PhoneNumber,
                    d.Email,
                    d.IsActive,
                    d.SafetyScore,                                        
                    d.Company == null ? null : new CompanyDto(
                        d.Company.Name,
                        d.Company.RepresentativeName,
                        d.Company.RepresentativeEmail,
                        d.Company.RepresentativePhone
                    ),
                    d.FamilyMembers.Select(fm => new FamilyMemberDto(
                        fm.FullName,
                        fm.PhoneNumber,
                        fm.Email,
                        fm.Relationship
                    )).ToList()
                )
                {
                    PictureUrl = d.PictureUrl
                })                                             
                .FirstOrDefaultAsync(cancellationToken);

            if (driverDto is null)
                return Result<GetDriverProfileResponse>.Failure<GetDriverProfileResponse>(new Error("Driver.NotFound", $"Driver with ID {request.DriverId} was not found."));
            
            if(!string.IsNullOrEmpty(driverDto.PictureUrl))
                driverDto.PictureUrl = _fileStorageService.GetPresignedUrl(driverDto.PictureUrl);

            return Result<GetDriverProfileResponse>.Success(driverDto);  
        }
    }
}