using System;
using System.Collections.Generic;

namespace ContentOS.Domain.Entities;

public class ArticleChatThread
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string Title { get; set; } = string.Empty;

    public virtual ICollection<ArticleChatMessage> Messages { get; set; } = new List<ArticleChatMessage>();
    public virtual ICollection<ArticleEditRequest> EditRequests { get; set; } = new List<ArticleEditRequest>();
}
