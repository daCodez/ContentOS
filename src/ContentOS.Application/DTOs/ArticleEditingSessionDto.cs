namespace ContentOS.Application.DTOs;

public class ArticleEditingSessionDto
{
    public Guid ArticleId { get; set; }
    public Guid ThreadId { get; set; }
    public IReadOnlyList<ArticleChatMessageDto> Messages { get; set; } = Array.Empty<ArticleChatMessageDto>();
    public IReadOnlyList<ArticleEditRequestDto> PendingRequests { get; set; } = Array.Empty<ArticleEditRequestDto>();
    public IReadOnlyList<ArticleRevisionDto> Revisions { get; set; } = Array.Empty<ArticleRevisionDto>();
    public ArticleFieldLocksDto FieldLocks { get; set; } = new();
}
