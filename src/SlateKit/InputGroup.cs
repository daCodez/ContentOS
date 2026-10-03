using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SlateKit;

public class InputGroup : ComponentBase
{
	public class IconButton
	{
		public RenderFragment IconContent { get; set; } = null;

		public string AriaLabel { get; set; } = "";

		public EventCallback<MouseEventArgs> OnClick { get; set; } = default(EventCallback<MouseEventArgs>);
	}

	private bool _focused;

	[Parameter]
	public string Placeholder { get; set; } = "";

	[Parameter]
	public string Value { get; set; } = "";

	[Parameter]
	public EventCallback<string> ValueChanged { get; set; }

	[Parameter]
	public string InputType { get; set; } = "text";

	[Parameter]
	public string Prefix { get; set; } = "";

	[Parameter]
	public string Suffix { get; set; } = "";

	[Parameter]
	public RenderFragment ChildContent { get; set; } = null;

	[Parameter]
	public IconButton SuffixIcon { get; set; }

	[Parameter]
	public EventCallback<bool> FocusChanged { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "input-group");
		__builder.AddAttribute(2, "onfocusin", EventCallback.Factory.Create<FocusEventArgs>((object)this, (Func<Task>)OnFocusIn));
		__builder.AddAttribute(3, "onfocusout", EventCallback.Factory.Create<FocusEventArgs>((object)this, (Func<Task>)OnFocusOut));
		if (!string.IsNullOrEmpty(Prefix))
		{
			__builder.OpenElement(4, "div");
			__builder.AddAttribute(5, "class", "input-group-prefix");
			__builder.AddContent(6, Prefix);
			__builder.CloseElement();
		}
		__builder.OpenElement(7, "input");
		__builder.AddAttribute(8, "type", InputType);
		__builder.AddAttribute(9, "class", "input");
		__builder.AddAttribute(10, "placeholder", Placeholder);
		__builder.AddAttribute(11, "value", BindConverter.FormatValue(Value));
		__builder.AddAttribute(12, "oninput", EventCallback.Factory.CreateBinder(this, delegate(string? __value)
		{
			Value = __value;
		}, Value));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		if (!string.IsNullOrEmpty(Suffix))
		{
			__builder.OpenElement(13, "div");
			__builder.AddAttribute(14, "class", "input-group-suffix");
			__builder.AddContent(15, Suffix);
			__builder.CloseElement();
		}
		if (SuffixIcon != null)
		{
			__builder.OpenElement(16, "button");
			__builder.AddAttribute(17, "class", "input-group-icon-btn");
			__builder.AddAttribute(18, "onclick", EventCallback.Factory.Create(this, SuffixIcon.OnClick));
			__builder.AddAttribute(19, "aria-label", SuffixIcon.AriaLabel);
			__builder.AddContent(20, SuffixIcon.IconContent);
			__builder.CloseElement();
		}
		__builder.CloseElement();
	}

	private async Task OnFocusIn()
	{
		_focused = true;
		await FocusChanged.InvokeAsync(arg: true);
	}

	private async Task OnFocusOut()
	{
		_focused = false;
		await FocusChanged.InvokeAsync(arg: false);
	}

	protected override void OnParametersSet()
	{
	}

	private async Task OnInputChange(ChangeEventArgs e)
	{
		if (e.Value != null)
		{
			Value = e.Value.ToString();
			await ValueChanged.InvokeAsync(Value);
		}
	}
}
