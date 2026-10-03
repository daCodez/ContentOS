using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class DropdownMenu : ComponentBase
{
	private ElementReference? menuElement;

	private string _menuClass = "menu";

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public RenderFragment? MenuContent { get; set; }

	[Parameter]
	public string? Header { get; set; }

	[Parameter]
	public bool RightAlign { get; set; }

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "menu-anchor");
		__builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Func<Task>)ToggleMenu));
		__builder.AddContent(3, ChildContent);
		__builder.AddMarkupContent(4, "\n    ");
		__builder.OpenElement(5, "div");
		__builder.AddAttribute(6, "class", _menuClass);
		__builder.AddEventStopPropagationAttribute(7, "onclick", value: true);
		__builder.AddElementReferenceCapture(8, delegate(ElementReference __value)
		{
			menuElement = __value;
		});
		if (!string.IsNullOrEmpty(Header))
		{
			__builder.OpenElement(9, "div");
			__builder.AddAttribute(10, "class", "menu-label");
			__builder.AddContent(11, Header);
			__builder.CloseElement();
		}
		__builder.AddContent(12, MenuContent);
		__builder.CloseElement();
		__builder.CloseElement();
	}

	private async Task ToggleMenu()
	{
		try
		{
			await JS.InvokeVoidAsync("contentOsInterop.toggleMenu", menuElement);
			if (RightAlign)
			{
				await JS.InvokeVoidAsync("contentOsInterop.addClass", menuElement, "menu-right");
			}
		}
		catch
		{
		}
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
	public void CloseMenu()
	{
		_menuClass = "menu";
		StateHasChanged();
	}

	[JSInvokable]
	public void Close()
	{
		CloseMenu();
	}

	public void Dispose()
	{
	}
}
