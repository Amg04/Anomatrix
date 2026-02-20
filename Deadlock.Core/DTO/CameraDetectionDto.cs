using Deadlock.Core.Domain.Entities;
using System.Text.Json.Serialization;

namespace Deadlock.Core.DTO
{
    public class CameraDetectionDto
    {
        public int Id { get; set; }
        [JsonPropertyName("camera_id")]
        public int CameraId { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("crowd_density")]
        public float Crowd_density { get; set; }
        [JsonPropertyName("activity_type")]
        public string Activity_type { get; set; } = string.Empty;
        [JsonPropertyName("threshold")]
        public float Threshold { get; set; }
        [JsonPropertyName("num_humans")]
        public int Num_humans { get; set; }
        [JsonPropertyName("weapon_count")]
        public int Weapon_count { get; set; }
        [JsonPropertyName("abnormal_count")]
        public int Abnormal_count { get; set; }
        [JsonPropertyName("is_crowded")]
        public bool Is_crowded { get; set; }
        
        #region Mapping

        public static explicit operator CameraDetection(CameraDetectionDto ViewModel)
        {
            return new CameraDetection
            {
                Id = ViewModel.Id,
                CameraId = ViewModel.CameraId,
                Status = ViewModel.Status,
                Crowd_density = ViewModel.Crowd_density,
                Activity_type = ViewModel.Activity_type,
                Threshold = ViewModel.Threshold,
                Num_humans = ViewModel.Num_humans,
                Weapon_count = ViewModel.Weapon_count,
                Abnormal_count = ViewModel.Abnormal_count,
                Is_crowded = ViewModel.Is_crowded,
            };
        }

        #endregion

    }
}
