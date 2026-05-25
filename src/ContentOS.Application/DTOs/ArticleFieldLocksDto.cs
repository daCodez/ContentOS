namespace ContentOS.Application.DTOs;

public class ArticleFieldLocksDto
{
    public bool TitleLocked { get; set; }
    public bool SummaryLocked { get; set; }
    public bool MetaDescriptionLocked { get; set; }
    public bool CallToActionLocked { get; set; }
}
