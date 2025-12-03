using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace AegisDrive.Api.Features.Drivers;


public static class DeleteDriver
{
    public record Command(int DriverId) : ICommand<Result<DeleteDriverResponse>>;


    internal sealed class Handler : IRequestHandler<Command, Result<DeleteDriverResponse>>
    {
        private readonly IGenericRepository<Driver, int> _DriverRepository;
        
        private readonly ILogger<Driver> _Logger;

        private readonly IFileStorageService _fileStorageService;
        
        public Handler(IGenericRepository<Driver, int> driverRepository, ILogger<Driver> Logger,  IFileStorageService fileStorageService)
        {
            _DriverRepository = driverRepository;
            _fileStorageService = fileStorageService;
            _Logger = Logger;
        }

        public async Task<Result<DeleteDriverResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var driver = await _DriverRepository
                .GetAll(d => d.Id == request.DriverId, trackChanges: false)
                .FirstOrDefaultAsync(cancellationToken);

            if (driver == null)
                return Result.Failure<DeleteDriverResponse>(new Error("DeleteDriver.NotFound", "Driver profile not found."));            
            try
            {
                if(!string.IsNullOrEmpty(driver.PictureUrl))
                  await _fileStorageService.DeleteAsync(driver.PictureUrl);
            }
            catch (Exception ex)
            {
                _Logger.Log(LogLevel.Warning,ex.Message);
            }

            await _DriverRepository.DeleteAsync(request.DriverId);


            return Result.Success(new DeleteDriverResponse(Success: true));
        }
    }

}
