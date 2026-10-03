using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class Dialog : ComponentBase
{
	private ElementReference? overlayElement;

	private ElementReference? dialogElement;

	private string _overlayClass = "dialog-overlay";

	[Parameter]
	public bool IsOpen { get; set; }

	[Parameter]
	public string Title { get; set; } = "";

	[Parameter]
	public string? Description { get; set; }

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public bool ShowCancelButton { get; set; } = true;

	[Parameter]
	public string CancelButtonText { get; set; } = "Cancel";

	[Parameter]
	public string ConfirmButtonText { get; set; } = "Confirm";

	[Parameter]
	public string ConfirmButtonClass { get; set; } = "btn-primary";

	[Parameter]
	public EventCallback OnConfirmCallback { get; set; }

	[Parameter]
	public EventCallback OnCancelCallback { get; set; }

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		if (IsOpen)
		{
			__builder.OpenElement(0, "div");
			__builder.AddAttribute(1, "class", _overlayClass);
			__builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnOverlayClick));
			__builder.AddElementReferenceCapture(3, delegate(ElementReference __value)
			{
				overlayElement = __value;
			});
			__builder.OpenElement(4, "div");
			__builder.AddAttribute(5, "class", "dialog");
			__builder.AddEventStopPropagationAttribute(6, "onclick", value: true);
			__builder.AddElementReferenceCapture(7, delegate(ElementReference __value)
			{
				dialogElement = __value;
			});
			__builder.OpenElement(8, "div");
			__builder.AddAttribute(9, "class", "dialog-header");
			__builder.OpenElement(10, "div");
			__builder.AddAttribute(11, "class", "dialog-title");
			__builder.AddContent(12, Title);
			__builder.CloseElement();
			if (!string.IsNullOrEmpty(Description))
			{
				__builder.OpenElement(13, "div");
				__builder.AddAttribute(14, "class", "dialog-description");
				__builder.AddContent(15, Description);
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.AddMarkupContent(16, "\n            ");
			__builder.OpenElement(17, "div");
			__builder.AddAttribute(18, "class", "dialog-body");
			__builder.AddContent(19, ChildContent);
			__builder.CloseElement();
			__builder.AddMarkupContent(20, "\n            ");
			__builder.OpenElement(21, "div");
			__builder.AddAttribute(22, "class", "dialog-footer");
			if (ShowCancelButton)
			{
				__builder.OpenElement(23, "button");
				__builder.AddAttribute(24, "class", "btn btn-outline");
				__builder.AddAttribute(25, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnCancel));
				__builder.AddContent(26, CancelButtonText);
				__builder.CloseElement();
			}
			if (!string.IsNullOrEmpty(ConfirmButtonText))
			{
				__builder.OpenElement(27, "button");
				__builder.AddAttribute(28, "class", "btn " + ConfirmButtonClass);
				__builder.AddAttribute(29, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnConfirm));
				__builder.AddContent(30, ConfirmButtonText);
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.CloseElement();
			__builder.CloseElement();
		}
	}

	[JSInvokable]
	public void Open()
	{
		_overlayClass = "dialog-overlay open";
		StateHasChanged();
	}

	[JSInvokable]
	public void Close()
	{
		_overlayClass = "dialog-overlay";
		StateHasChanged();
	}

	private void OnOverlayClick()
	{
		Close();
		OnCancelCallback.InvokeAsync(null);
	}

	private void OnCancel()
	{
		Close();
		OnCancelCallback.InvokeAsync(null);
	}

	private void OnConfirm()
	{
		Close();
		OnConfirmCallback.InvokeAsync(null);
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			try
			{
				await JS.InvokeVoidAsync("contentOsInterop.setupDialogEscape", DotNetObjectReference.Create(this));
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
