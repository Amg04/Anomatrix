using Deadlock.Core.Domain.Entities;
using Deadlock.Core.DTO;
using System.Security.Claims;

namespace Deadlock.Core.ServiceContracts
{
    public interface IJwtService
    {
        Task<AuthenticationResponse> CreateJwtToken(AppUser user);
        ClaimsPrincipal? GetPrincipalFromJwtToken(string? token);
    }
}
