namespace Deadlock.Core.DTO
{
    public class SiteSelectionDto
    {
        public IEnumerable<string> EntityTypes { get; set; } = new List<string>();
        public string  EntityName { get; set; } = string.Empty;
        public IEnumerable<YourSitesDto>? YourSites { get; set; }
    }
}
