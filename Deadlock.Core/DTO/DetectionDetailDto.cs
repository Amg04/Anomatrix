using System.Text.Json.Serialization;

namespace Deadlock.Core.DTO
{
    public class DetectionDetailDto
    {
        [JsonPropertyName("class_label")]
        public string Class_label { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("bbox")]
        public List<float> Bbox { get; set; } = new();

        [JsonPropertyName("is_abnormal")]
        public bool Is_abnormal { get; set; }

        [JsonPropertyName("speed")]
        public float Speed { get; set; }
    }
}
