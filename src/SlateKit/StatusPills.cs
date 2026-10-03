using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class StatusPills : ComponentBase
{
	[Parameter]
	public RenderFragment ChildContent { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "status-pills");
		__builder.AddContent(2, ChildContent);
		__builder.CloseElement();
	}
}
