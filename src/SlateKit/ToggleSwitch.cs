using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.CompilerServices;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace SlateKit;

public class ToggleSwitch : ComponentBase
{
	private bool _checked;

	[Parameter]
	public bool Checked { get; set; }

	[Parameter]
	public EventCallback<bool> CheckedChanged { get; set; }

	[Parameter]
	public string? Title { get; set; }

	[Parameter]
	public string? Description { get; set; }

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "switch-row");
		__builder.OpenElement(2, "label");
		__builder.AddAttribute(3, "class", "switch");
		__builder.OpenElement(4, "input");
		__builder.AddAttribute(5, "type", "checkbox");
		__builder.AddAttribute(6, "checked", BindConverter.FormatValue(_checked));
		__builder.AddAttribute(7, "onchange", EventCallback.Factory.CreateBinder(this, RuntimeHelpers.CreateInferredBindSetter(delegate(bool __value)
		{
			_checked = __value;
			return RuntimeHelpers.InvokeAsynchronousDelegate((Func<Task>)OnCheckedChanged);
		}, _checked), _checked));
		__builder.SetUpdatesAttributeName("checked");
		__builder.CloseElement();
		__builder.AddMarkupContent(8, "\n        <span class=\"switch-track\"></span>\n        <span class=\"switch-thumb\"></span>");
		__builder.CloseElement();
		if (!string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Description))
		{
			__builder.OpenElement(9, "div");
			__builder.AddAttribute(10, "class", "switch-row-body");
			if (!string.IsNullOrWhiteSpace(Title))
			{
				__builder.OpenElement(11, "div");
				__builder.AddAttribute(12, "class", "switch-row-title");
				__builder.AddContent(13, Title);
				__builder.CloseElement();
			}
			if (!string.IsNullOrWhiteSpace(Description))
			{
				__builder.OpenElement(14, "div");
				__builder.AddAttribute(15, "class", "switch-row-desc");
				__builder.AddContent(16, Description);
				__builder.CloseElement();
			}
			__builder.CloseElement();
		}
		__builder.CloseElement();
	}

	protected override void OnParametersSet()
	{
		_checked = Checked;
	}

	private async Task OnCheckedChanged()
	{
		await CheckedChanged.InvokeAsync(_checked);
	}
}
