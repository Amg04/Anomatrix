using System.Text.Json.Serialization;

namespace Deadlock.Core.Domain.Entities
{
    public class CameraDetection : BaseClass
    {
        public int CameraId { get; set; }
        public Camera Camera { get; set; } = null!;
        public string Status { get; set; } = string.Empty;
        public float Crowd_density { get; set; }
         public string Activity_type { get; set; } = string.Empty;
         public float Threshold { get; set; }
        public int Num_humans { get; set; }
        public int Weapon_count { get; set; }
        public int Abnormal_count { get; set; }
        public bool Is_crowded { get; set; }
    }
}
