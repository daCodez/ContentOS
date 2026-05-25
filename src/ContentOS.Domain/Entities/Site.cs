using System;

namespace ContentOS.Domain.Entities;

public class Site
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string PlatformType { get; set; } = "WordPress";
    public bool IsActive { get; set; } = true;
    public string DefaultTone { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
