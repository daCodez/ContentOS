using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class Sheet : ComponentBase
{
	private ElementReference? overlayElement;

	private ElementReference? sheetElement;

	private string _overlayClass = "sheet-overlay";

	private string _sheetClass = "sheet";

	private string sheetTitleId = Guid.NewGuid().ToString();

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
	public string ConfirmButtonText { get; set; } = "Save";

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
			__builder.CloseElement();
			__builder.AddMarkupContent(4, "\n    ");
			__builder.OpenElement(5, "aside");
			__builder.AddAttribute(6, "class", _sheetClass);
			__builder.AddAttribute(7, "role", "dialog");
			__builder.AddAttribute(8, "aria-labelledby", sheetTitleId);
			__builder.AddElementReferenceCapture(9, delegate(ElementReference __value)
			{
				sheetElement = __value;
			});
			__builder.OpenElement(10, "button");
			__builder.AddAttribute(11, "class", "sheet-close");
			__builder.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)Close));
			__builder.AddAttribute(13, "aria-label", "Close");
			__builder.AddMarkupContent(14, "<svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><line x1=\"18\" y1=\"6\" x2=\"6\" y2=\"18\"></line><line x1=\"6\" y1=\"6\" x2=\"18\" y2=\"18\"></line></svg>");
			__builder.CloseElement();
			__builder.AddMarkupContent(15, "\n        ");
			__builder.OpenElement(16, "div");
			__builder.AddAttribute(17, "class", "sheet-header");
			__builder.OpenElement(18, "div");
			__builder.AddAttribute(19, "class", "sheet-title");
			__builder.AddAttribute(20, "id", sheetTitleId);
			__builder.AddContent(21, Title);
			__builder.CloseElement();
			if (!string.IsNullOrEmpty(Description))
			{
				__builder.OpenElement(22, "div");
				__builder.AddAttribute(23, "class", "sheet-description");
				__builder.AddContent(24, Description);
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.AddMarkupContent(25, "\n        ");
			__builder.OpenElement(26, "div");
			__builder.AddAttribute(27, "class", "sheet-body");
			__builder.AddContent(28, ChildContent);
			__builder.CloseElement();
			__builder.AddMarkupContent(29, "\n        ");
			__builder.OpenElement(30, "div");
			__builder.AddAttribute(31, "class", "sheet-footer");
			if (ShowCancelButton)
			{
				__builder.OpenElement(32, "button");
				__builder.AddAttribute(33, "class", "btn btn-outline");
				__builder.AddAttribute(34, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnCancel));
				__builder.AddContent(35, CancelButtonText);
				__builder.CloseElement();
			}
			if (!string.IsNullOrEmpty(ConfirmButtonText))
			{
				__builder.OpenElement(36, "button");
				__builder.AddAttribute(37, "class", "btn " + ConfirmButtonClass);
				__builder.AddAttribute(38, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnConfirm));
				__builder.AddContent(39, ConfirmButtonText);
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.CloseElement();
		}
	}

	[JSInvokable]
	public void Open()
	{
		_overlayClass = "sheet-overlay open";
		_sheetClass = "sheet open";
		StateHasChanged();
	}

	[JSInvokable]
	public void Close()
	{
		_overlayClass = "sheet-overlay";
		_sheetClass = "sheet";
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
				await JS.InvokeVoidAsync("contentOsInterop.setupSheetEscape", DotNetObjectReference.Create(this));
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
