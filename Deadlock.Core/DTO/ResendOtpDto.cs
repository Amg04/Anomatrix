using System.ComponentModel.DataAnnotations;

namespace Deadlock.Core.DTO
{
    public class ResendOtpDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
