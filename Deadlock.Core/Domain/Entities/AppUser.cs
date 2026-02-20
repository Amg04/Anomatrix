using Microsoft.AspNetCore.Identity;

namespace Deadlock.Core.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string? Name { get; set; }
        public string? ImgUrl { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpirationDateTime { get; set; }
        public string? OtpCode { get; set; }
        public DateTime OtpExpiration { get; set; }
        public string? ManagerId { get; set; }
        public AppUser? Manager { get; set; }
        public ICollection<AppUser> Subordinates { get; set; } = new HashSet<AppUser>();
    }
}
