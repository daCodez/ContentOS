using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class Tooltip : ComponentBase
{
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public string Text { get; set; } = "";

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "tooltip-anchor");
		__builder.AddContent(2, ChildContent);
		__builder.AddMarkupContent(3, "\n    ");
		__builder.OpenElement(4, "div");
		__builder.AddAttribute(5, "class", "tooltip");
		__builder.AddContent(6, Text);
		__builder.CloseElement();
		__builder.CloseElement();
	}
}
