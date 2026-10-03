using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class CommandPalette : ComponentBase
{
	public record CommandItem(string Group, string Label, string? Shortcut = null);

	[Parameter]
	public List<CommandItem> Items { get; set; } = new List<CommandItem>();

	[Parameter]
	public string Placeholder { get; set; } = "Search…";

	[Parameter]
	public EventCallback<CommandItem> OnSelect { get; set; }

	private bool IsOpen { get; set; }

	private string Query { get; set; } = "";

	public IEnumerable<CommandItem> FilteredItems
	{
		get
		{
			IEnumerable<CommandItem> result;
			if (!string.IsNullOrWhiteSpace(Query))
			{
				result = Items.Where((CommandItem i) => i.Label.Contains(Query, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				IEnumerable<CommandItem> items = Items;
				result = items;
			}
			return result;
		}
	}

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "cmd-overlay " + (IsOpen ? "open" : ""));
		__builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)CloseFromOverlay));
		__builder.OpenElement(3, "div");
		__builder.AddAttribute(4, "class", "cmd-dialog");
		__builder.AddEventStopPropagationAttribute(5, "onclick", value: true);
		__builder.OpenElement(6, "div");
		__builder.AddAttribute(7, "class", "cmd-input-wrap");
		__builder.AddMarkupContent(8, "<svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><circle cx=\"11\" cy=\"11\" r=\"8\"></circle><line x1=\"21\" y1=\"21\" x2=\"16.65\" y2=\"16.65\"></line></svg>\n            ");
		__builder.OpenElement(9, "input");
		__builder.AddAttribute(10, "type", "text");
		__builder.AddAttribute(11, "class", "cmd-input");
		__builder.AddAttribute(12, "placeholder", Placeholder);
		__builder.AddAttribute(13, "value", BindConverter.FormatValue(Query));
		__builder.AddAttribute(14, "oninput", EventCallback.Factory.CreateBinder(this, delegate(string? __value)
		{
			Query = __value;
		}, Query));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.AddMarkupContent(15, "\n        ");
		__builder.OpenElement(16, "div");
		__builder.AddAttribute(17, "class", "cmd-list");
		if (FilteredItems.Any())
		{
			foreach (IGrouping<string, CommandItem> item2 in from i in FilteredItems
				group i by i.Group)
			{
				__builder.OpenElement(18, "div");
				__builder.AddAttribute(19, "class", "cmd-group-label");
				__builder.AddContent(20, item2.Key);
				__builder.CloseElement();
				foreach (CommandItem item in item2)
				{
					__builder.OpenElement(21, "div");
					__builder.AddAttribute(22, "class", "cmd-item");
					__builder.AddAttribute(23, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => SelectItem(item)));
					__builder.OpenElement(24, "span");
					__builder.AddContent(25, item.Label);
					__builder.CloseElement();
					if (!string.IsNullOrEmpty(item.Shortcut))
					{
						__builder.OpenElement(26, "span");
						__builder.AddAttribute(27, "class", "cmd-item-shortcut");
						__builder.AddContent(28, item.Shortcut);
						__builder.CloseElement();
					}
					__builder.CloseElement();
				}
			}
		}
		else
		{
			__builder.AddMarkupContent(29, "<div class=\"cmd-empty\">No results found.</div>");
		}
		__builder.CloseElement();
		__builder.AddMarkupContent(30, "\n        ");
		__builder.AddMarkupContent(31, "<div class=\"cmd-footer\"><span><kbd>&uarr;</kbd><kbd>&darr;</kbd> Navigate</span>\n            <span><kbd>&#8617;</kbd> Select</span>\n            <span><kbd>Esc</kbd> Close</span></div>");
		__builder.CloseElement();
		__builder.CloseElement();
	}

	public async Task Open()
	{
		IsOpen = true;
		Query = "";
		StateHasChanged();
		try
		{
			await JS.InvokeVoidAsync("contentOsInterop.openCommandPalette");
		}
		catch
		{
		}
	}

	public void Close()
	{
		IsOpen = false;
		Query = "";
		StateHasChanged();
	}

	private void CloseFromOverlay()
	{
		Close();
	}

	private async Task SelectItem(CommandItem item)
	{
		Close();
		await OnSelect.InvokeAsync(item);
	}
}
