using System.ComponentModel.DataAnnotations;

namespace Deadlock.Core.DTO
{
    public class SiteSelectionRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string EntityType { get; set; } = string.Empty;
        [Required]
        public string EntityName { get; set; } = string.Empty;
    }
}
