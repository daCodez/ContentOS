using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class HoverCard : ComponentBase
{
	public class HoverCardMeta
	{
		public string Label { get; set; } = "";

		public string Value { get; set; } = "";
	}

	private ElementReference? triggerElement;

	private ElementReference? hoverCardElement;

	private string _hoverCardClass = "hover-card";

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public string Title { get; set; } = "";

	[Parameter]
	public string? Snippet { get; set; }

	[Parameter]
	public string Status { get; set; } = "";

	[Parameter]
	public List<HoverCardMeta> MetaItems { get; set; } = new List<HoverCardMeta>();

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "hover-trigger");
		__builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)OnClick));
		__builder.AddElementReferenceCapture(3, delegate(ElementReference __value)
		{
			triggerElement = __value;
		});
		__builder.AddContent(4, ChildContent);
		__builder.AddMarkupContent(5, "\n    ");
		__builder.OpenElement(6, "div");
		__builder.AddAttribute(7, "class", _hoverCardClass);
		__builder.AddElementReferenceCapture(8, delegate(ElementReference __value)
		{
			hoverCardElement = __value;
		});
		__builder.OpenElement(9, "div");
		__builder.AddAttribute(10, "class", "hover-card-head");
		__builder.OpenElement(11, "div");
		__builder.AddAttribute(12, "class", "hover-card-title");
		__builder.AddContent(13, Title);
		__builder.CloseElement();
		if (!string.IsNullOrEmpty(Status))
		{
			__builder.OpenElement(14, "span");
			__builder.AddAttribute(15, "class", "badge badge-" + Status.ToLowerInvariant());
			__builder.AddContent(16, Status);
			__builder.CloseElement();
		}
		__builder.CloseElement();
		if (!string.IsNullOrEmpty(Snippet))
		{
			__builder.OpenElement(17, "div");
			__builder.AddAttribute(18, "class", "hover-card-snippet");
			__builder.AddContent(19, Snippet);
			__builder.CloseElement();
		}
		if (MetaItems != null && MetaItems.Any())
		{
			__builder.OpenElement(20, "div");
			__builder.AddAttribute(21, "class", "hover-card-meta");
			foreach (HoverCardMeta metaItem in MetaItems)
			{
				__builder.OpenElement(22, "span");
				__builder.OpenElement(23, "strong");
				__builder.AddContent(24, metaItem.Label);
				__builder.CloseElement();
				__builder.AddContent(25, " ");
				__builder.AddContent(26, metaItem.Value);
				__builder.CloseElement();
			}
			__builder.CloseElement();
		}
		__builder.CloseElement();
		__builder.CloseElement();
	}

	[JSInvokable]
	public void Show()
	{
		_hoverCardClass = "hover-card show";
		StateHasChanged();
	}

	[JSInvokable]
	public void Hide()
	{
		_hoverCardClass = "hover-card";
		StateHasChanged();
	}

	public void Dispose()
	{
	}

	private void OnClick()
	{
	}
}
