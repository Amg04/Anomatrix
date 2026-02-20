using Deadlock.Core.Enums;

namespace Deadlock.Core.Domain.Entities
{
    public class MonitoredEntity : BaseClass
    {
        public EntityTypes EntityType { get; set; } = default!;
        public string EntityName { get; set; } = default!;
        public string? Location { get; set; } 
        public DateTime LastUpdate { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public AppUser User { get; set; } = null!;
        public ICollection<Camera> Cameras { get; set; } = new HashSet<Camera>();
    }

}
