using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class SelectList : ComponentBase
{
	public class SelectOption
	{
		public string Text { get; set; } = "";

		public string Value { get; set; } = "";

		public string? IconHtml { get; set; }
	}

	private ElementReference? anchorElement;

	private ElementReference? triggerElement;

	private ElementReference? listElement;

	private string _selectListClass = "select-list";

	[Parameter]
	public List<SelectOption> Items { get; set; } = new List<SelectOption>();

	[Parameter]
	public string? Placeholder { get; set; } = "Select an option";

	[Parameter]
	public EventCallback<string> SelectedValueChanged { get; set; }

	[Parameter]
	public string? SelectedValue { get; set; }

	private SelectOption? SelectedItem => Items.FirstOrDefault((SelectOption i) => i.Value.Equals(SelectedValue, StringComparison.OrdinalIgnoreCase));

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "select-anchor");
		__builder.AddElementReferenceCapture(2, delegate(ElementReference __value)
		{
			anchorElement = __value;
		});
		__builder.OpenElement(3, "div");
		__builder.AddAttribute(4, "class", "select-trigger");
		__builder.AddAttribute(5, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Func<Task>)ToggleSelect));
		__builder.AddElementReferenceCapture(6, delegate(ElementReference __value)
		{
			triggerElement = __value;
		});
		if (SelectedItem != null)
		{
			__builder.OpenElement(7, "span");
			__builder.AddAttribute(8, "class", "select-value");
			__builder.AddContent(9, SelectedItem.Text);
			__builder.CloseElement();
		}
		else
		{
			__builder.OpenElement(10, "span");
			__builder.AddAttribute(11, "class", "select-value placeholder");
			__builder.AddContent(12, Placeholder);
			__builder.CloseElement();
		}
		__builder.AddMarkupContent(13, "<svg class=\"chevron\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><polyline points=\"6 9 12 15 18 9\"></polyline></svg>");
		__builder.CloseElement();
		__builder.AddMarkupContent(14, "\n    ");
		__builder.OpenElement(15, "div");
		__builder.AddAttribute(16, "class", _selectListClass);
		__builder.AddEventStopPropagationAttribute(17, "onclick", value: true);
		__builder.AddElementReferenceCapture(18, delegate(ElementReference __value)
		{
			listElement = __value;
		});
		if (Items == null || Items.Count == 0)
		{
			__builder.AddMarkupContent(19, "<div class=\"select-item\">No options</div>");
		}
		else
		{
			foreach (SelectOption item in Items)
			{
				__builder.OpenElement(20, "div");
				__builder.AddAttribute(21, "class", "select-item " + (item.Value.Equals(SelectedItem?.Value.ToString(), StringComparison.OrdinalIgnoreCase) ? "selected" : ""));
				__builder.AddAttribute(22, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)delegate
				{
					OnSelectItem(item);
				}));
				__builder.AddAttribute(23, "data-value", item.Value);
				if (!string.IsNullOrEmpty(item.IconHtml))
				{
					__builder.OpenElement(24, "span");
					__builder.AddAttribute(25, "class", "check");
					__builder.AddContent(26, item.IconHtml);
					__builder.CloseElement();
				}
				__builder.OpenElement(27, "span");
				__builder.AddContent(28, item.Text);
				__builder.CloseElement();
				__builder.CloseElement();
			}
		}
		__builder.CloseElement();
		__builder.CloseElement();
	}

	[JSInvokable]
	public void Open()
	{
		_selectListClass = "select-list open";
		StateHasChanged();
	}

	[JSInvokable]
	public void Close()
	{
		_selectListClass = "select-list";
		StateHasChanged();
	}

	private async Task ToggleSelect()
	{
		await JS.InvokeVoidAsync("contentOsInterop.toggleSelect", triggerElement, listElement);
	}

	private void OnSelectItem(SelectOption item)
	{
		SelectedValue = item.Value;
		SelectedValueChanged.InvokeAsync(SelectedValue);
		Close();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			try
			{
				await JS.InvokeVoidAsync("contentOsInterop.setupSelectClickOutside", DotNetObjectReference.Create(this));
			}
			catch
			{
			}
		}
	}

	public void Dispose()
	{
	}
}
