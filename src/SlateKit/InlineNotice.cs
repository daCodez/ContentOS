using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SlateKit;

public class InlineNotice : ComponentBase
{
	[Parameter]
	public string Title { get; set; } = "";

	[Parameter]
	public string Description { get; set; } = "";

	[Parameter]
	public bool ShowActionButton { get; set; } = false;

	[Parameter]
	public string ActionButtonText { get; set; } = "Action";

	[Parameter]
	public EventCallback OnActionClick { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "inline-notice");
		__builder.OpenElement(2, "div");
		__builder.AddAttribute(3, "class", "inline-notice-body");
		if (!string.IsNullOrWhiteSpace(Title))
		{
			__builder.OpenElement(4, "div");
			__builder.AddAttribute(5, "class", "inline-notice-title");
			__builder.AddContent(6, Title);
			__builder.CloseElement();
		}
		if (!string.IsNullOrWhiteSpace(Description))
		{
			__builder.OpenElement(7, "div");
			__builder.AddAttribute(8, "class", "inline-notice-desc");
			__builder.AddContent(9, Description);
			__builder.CloseElement();
		}
		__builder.CloseElement();
		if (ShowActionButton)
		{
			__builder.OpenElement(10, "button");
			__builder.AddAttribute(11, "class", "btn btn-outline btn-sm");
			__builder.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnActionClick));
			__builder.AddContent(13, ActionButtonText);
			__builder.CloseElement();
		}
		__builder.CloseElement();
	}
}
