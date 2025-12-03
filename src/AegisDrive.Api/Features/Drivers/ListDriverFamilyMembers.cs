using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Drivers;


public static class ListDriverFamilyMembers
{

    public record Query(int Id) : IRequest<Result<IEnumerable<GetDriverFamilyMembersResponse>>>;


    internal sealed class Handler : IRequestHandler<Query, Result<IEnumerable<GetDriverFamilyMembersResponse>>>
    {
        private readonly IGenericRepository<FamilyMember, int> _familyMembersRepository;

        public Handler(IGenericRepository<FamilyMember, int> familyMembersRepository)
        {
            _familyMembersRepository = familyMembersRepository;
        }
        public async Task<Result<IEnumerable<GetDriverFamilyMembersResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var driverFamilyMembersDtos = await _familyMembersRepository
                .GetAll(fm => fm.DriverId == request.Id)
                .Select(Fm => new GetDriverFamilyMembersResponse(Fm.FullName, Fm.PhoneNumber, Fm.Email, Fm.Relationship,Fm.NotifyOnCritical ,   Fm.DriverId)
                ).ToListAsync();

            if (driverFamilyMembersDtos is null)
                return Result<IEnumerable<GetDriverFamilyMembersResponse>>.Failure<IEnumerable<GetDriverFamilyMembersResponse>>(new Error("ListDriverFamilyMembers.Failure", "this driver does not have any Family Members Associated with him"));


           return Result<IEnumerable<GetDriverFamilyMembersResponse>>.Success<IEnumerable<GetDriverFamilyMembersResponse>>(driverFamilyMembersDtos);
               
        }
    }



}
