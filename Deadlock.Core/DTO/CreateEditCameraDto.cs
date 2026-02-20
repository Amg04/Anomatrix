using System.ComponentModel.DataAnnotations;

namespace Deadlock.Core.DTO
{
    public class CreateEditCameraDto
    {
        public string? Host { get; set; } 
        public int Port { get; set; } = 554;
        public string? Username { get; set; } 
        public string? PasswordEnc { get; set; }
        [Required]
        public string RtspPath { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string? CameraLocation { get; set; }
    }
}
