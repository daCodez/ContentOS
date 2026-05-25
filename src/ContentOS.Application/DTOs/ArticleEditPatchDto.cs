namespace ContentOS.Application.DTOs;

public class ArticleEditPatchDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string BeforeContent { get; set; } = string.Empty;
    public string ProposedContent { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string Warnings { get; set; } = string.Empty;
    public bool FieldLocksRespected { get; set; }
    public int SortOrder { get; set; }
}
