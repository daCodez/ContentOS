using System;

namespace ContentOS.Domain.Entities;

public class ArticleChatMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid? RequestId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public virtual ArticleChatThread Thread { get; set; } = null!;
    public virtual ArticleEditRequest? Request { get; set; }
}
