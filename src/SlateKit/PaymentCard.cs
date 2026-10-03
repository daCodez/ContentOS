using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class PaymentCard : ComponentBase
{
	[Parameter]
	public string Title { get; set; } = "";

	[Parameter]
	public string Description { get; set; } = "";

	[Parameter]
	public RenderFragment ChildContent { get; set; } = null;

	[Parameter]
	public bool ShowFooter { get; set; } = true;

	[Parameter]
	public RenderFragment FooterContent { get; set; } = null;

	[Parameter]
	public string ContainerStyle { get; set; } = "max-width: 520px";

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "payment-card");
		__builder.AddAttribute(2, "style", ContainerStyle);
		__builder.OpenElement(3, "div");
		__builder.AddAttribute(4, "class", "payment-card-head");
		__builder.OpenElement(5, "div");
		__builder.AddAttribute(6, "class", "payment-card-title");
		__builder.AddContent(7, Title);
		__builder.CloseElement();
		if (!string.IsNullOrWhiteSpace(Description))
		{
			__builder.OpenElement(8, "div");
			__builder.AddAttribute(9, "class", "payment-card-desc");
			__builder.AddContent(10, Description);
			__builder.CloseElement();
		}
		__builder.CloseElement();
		__builder.AddMarkupContent(11, "\n    ");
		__builder.OpenElement(12, "div");
		__builder.AddAttribute(13, "class", "payment-card-body");
		__builder.AddContent(14, ChildContent);
		__builder.CloseElement();
		if (ShowFooter)
		{
			__builder.OpenElement(15, "div");
			__builder.AddAttribute(16, "class", "payment-card-foot");
			__builder.AddContent(17, FooterContent);
			__builder.CloseElement();
		}
		__builder.CloseElement();
	}
}
