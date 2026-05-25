using System;

namespace ContentOS.Domain.Entities;

public class PublishRecord
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public DateTime PublishedAt { get; set; }
    public string PublishedBy { get; set; } = string.Empty;
    public string PublicationChannel { get; set; } = string.Empty;
    
    public virtual Article Article { get; set; } = null!;
}