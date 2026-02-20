using System.ComponentModel.DataAnnotations;

namespace Deadlock.Core.DTO
{
    public class ForgotPasswordRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
