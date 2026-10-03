using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SlateKit;

public class SuccessBanner : ComponentBase
{
	[Parameter]
	public string Message { get; set; } = "Success";

	[Parameter]
	public EventCallback OnClick { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "success-banner");
		__builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnClick));
		__builder.AddMarkupContent(3, "<div class=\"success-banner-icon\"><svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"20 6 9 17 4 12\"></polyline></svg></div>\n    ");
		__builder.OpenElement(4, "div");
		__builder.AddAttribute(5, "class", "success-banner-text");
		__builder.AddContent(6, Message);
		__builder.CloseElement();
		__builder.AddMarkupContent(7, "\n    ");
		__builder.AddMarkupContent(8, "<div class=\"success-banner-arrow\"><svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><polyline points=\"9 18 15 12 9 6\"></polyline></svg></div>");
		__builder.CloseElement();
	}
}
