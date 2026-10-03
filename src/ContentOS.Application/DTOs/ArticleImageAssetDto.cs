namespace ContentOS.Application.DTOs;

public class ArticleImageAssetDto
{
	public string ImagePath { get; set; } = string.Empty;

	public string AltText { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string ImageRole { get; set; } = string.Empty;

	public string SuggestedPlacement { get; set; } = string.Empty;

	public bool IsFeatured { get; set; }
}
