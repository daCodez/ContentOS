using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class SearchWithCount : ComponentBase
{
	[Parameter]
	public string Placeholder { get; set; } = "Search...";

	[Parameter]
	public string ResultCountText { get; set; } = "";

	[Parameter]
	public string SearchTerm { get; set; } = "";

	[Parameter]
	public EventCallback<string> SearchTermChanged { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "search-with-count");
		__builder.AddMarkupContent(2, "<svg class=\"search-with-count-icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"11\" cy=\"11\" r=\"8\"></circle><line x1=\"21\" y1=\"21\" x2=\"16.65\" y2=\"16.65\"></line></svg>\n    ");
		__builder.OpenElement(3, "input");
		__builder.AddAttribute(4, "type", "text");
		__builder.AddAttribute(5, "class", "input");
		__builder.AddAttribute(6, "placeholder", Placeholder);
		__builder.AddAttribute(7, "value", BindConverter.FormatValue(SearchTerm));
		__builder.AddAttribute(8, "onchange", EventCallback.Factory.CreateBinder(this, delegate(string? __value)
		{
			SearchTerm = __value;
		}, SearchTerm));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		if (!string.IsNullOrEmpty(ResultCountText))
		{
			__builder.OpenElement(9, "span");
			__builder.AddAttribute(10, "class", "search-with-count-result");
			__builder.AddContent(11, ResultCountText);
			__builder.CloseElement();
		}
		__builder.CloseElement();
	}

	private async Task OnSearchTermChanged(ChangeEventArgs e)
	{
		if (e.Value != null)
		{
			SearchTerm = e.Value.ToString();
			await SearchTermChanged.InvokeAsync(SearchTerm);
		}
	}
}
