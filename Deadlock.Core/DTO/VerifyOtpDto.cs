using System.ComponentModel.DataAnnotations;

namespace Deadlock.Core.DTO
{
    public class VerifyOtpDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required, StringLength(4)]
        public string Otp { get; set; } = string.Empty;
    }
}
