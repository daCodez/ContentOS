using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class LabelDivider : ComponentBase
{
	[Parameter]
	public string Label { get; set; } = "";

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "label-divider");
		__builder.OpenElement(2, "div");
		__builder.AddAttribute(3, "class", "label-divider-text");
		__builder.AddContent(4, Label);
		__builder.CloseElement();
		__builder.CloseElement();
	}
}
