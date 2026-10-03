using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class Popover : ComponentBase
{
	public class PopoverRow
	{
		public string Label { get; set; } = "";

		public string Value { get; set; } = "";
	}

	private ElementReference? anchorElement;

	private ElementReference? popoverElement;

	private string _popoverClass = "popover";

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public string Title { get; set; } = "";

	[Parameter]
	public string? Description { get; set; }

	[Parameter]
	public List<PopoverRow> Rows { get; set; } = new List<PopoverRow>();

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "popover-anchor");
		__builder.AddElementReferenceCapture(2, delegate(ElementReference __value)
		{
			anchorElement = __value;
		});
		__builder.AddContent(3, ChildContent);
		__builder.AddMarkupContent(4, "\n    ");
		__builder.OpenElement(5, "div");
		__builder.AddAttribute(6, "class", _popoverClass);
		__builder.AddEventStopPropagationAttribute(7, "onclick", value: true);
		__builder.AddElementReferenceCapture(8, delegate(ElementReference __value)
		{
			popoverElement = __value;
		});
		if (!string.IsNullOrEmpty(Title))
		{
			__builder.OpenElement(9, "div");
			__builder.AddAttribute(10, "class", "popover-title");
			__builder.AddContent(11, Title);
			__builder.CloseElement();
		}
		if (!string.IsNullOrEmpty(Description))
		{
			__builder.OpenElement(12, "div");
			__builder.AddAttribute(13, "class", "popover-desc");
			__builder.AddContent(14, Description);
			__builder.CloseElement();
		}
		foreach (PopoverRow row in Rows)
		{
			__builder.OpenElement(15, "div");
			__builder.AddAttribute(16, "class", "popover-row");
			__builder.OpenElement(17, "span");
			__builder.AddAttribute(18, "class", "label");
			__builder.AddContent(19, row.Label);
			__builder.CloseElement();
			__builder.AddMarkupContent(20, "\n                ");
			__builder.OpenElement(21, "span");
			__builder.AddAttribute(22, "class", "value");
			__builder.AddContent(23, row.Value);
			__builder.CloseElement();
			__builder.CloseElement();
		}
		__builder.CloseElement();
		__builder.CloseElement();
	}

	[JSInvokable]
	public void Open()
	{
		_popoverClass = "popover open";
		StateHasChanged();
	}

	[JSInvokable]
	public void Close()
	{
		_popoverClass = "popover";
		StateHasChanged();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			try
			{
				await JS.InvokeVoidAsync("contentOsInterop.setupClickOutside", DotNetObjectReference.Create(this));
			}
			catch
			{
			}
		}
	}

	[JSInvokable]
	public void Toggle()
	{
		if (_popoverClass.Contains("open"))
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public void Dispose()
	{
	}
}
