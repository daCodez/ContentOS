using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class RangeSlider : ComponentBase
{
	[Parameter]
	public string Title { get; set; } = "Range";

	[Parameter]
	public string Description { get; set; } = "";

	[Parameter]
	[EditorRequired]
	public int MinValue { get; set; } = 0;

	[Parameter]
	[EditorRequired]
	public int MaxValue { get; set; } = 100;

	[Parameter]
	public EventCallback<int> MinValueChanged { get; set; }

	[Parameter]
	public EventCallback<int> MaxValueChanged { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "range-block");
		__builder.OpenElement(2, "div");
		__builder.AddAttribute(3, "class", "range-head");
		__builder.OpenElement(4, "div");
		__builder.AddAttribute(5, "class", "range-title");
		__builder.AddContent(6, Title);
		__builder.CloseElement();
		if (!string.IsNullOrWhiteSpace(Description))
		{
			__builder.OpenElement(7, "div");
			__builder.AddAttribute(8, "class", "range-desc");
			__builder.AddContent(9, Description);
			__builder.CloseElement();
		}
		__builder.CloseElement();
		__builder.AddMarkupContent(10, "\n    ");
		__builder.OpenElement(11, "div");
		__builder.AddAttribute(12, "class", "range-inputs");
		__builder.OpenElement(13, "div");
		__builder.AddAttribute(14, "class", "range-input");
		__builder.AddMarkupContent(15, "<label>Min</label>\n            ");
		__builder.OpenElement(16, "input");
		__builder.AddAttribute(17, "type", "range");
		__builder.AddAttribute(18, "min", "0");
		__builder.AddAttribute(19, "max", "100");
		__builder.AddAttribute(20, "value", BindConverter.FormatValue(MinValue));
		__builder.AddAttribute(21, "oninput", EventCallback.Factory.CreateBinder(this, delegate(int __value)
		{
			MinValue = __value;
		}, MinValue));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		__builder.AddMarkupContent(22, "\n            ");
		__builder.OpenElement(23, "span");
		__builder.AddAttribute(24, "class", "range-value");
		__builder.AddContent(25, MinValue);
		__builder.AddContent(26, "%");
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.AddMarkupContent(27, "\n        ");
		__builder.OpenElement(28, "div");
		__builder.AddAttribute(29, "class", "range-input");
		__builder.AddMarkupContent(30, "<label>Max</label>\n            ");
		__builder.OpenElement(31, "input");
		__builder.AddAttribute(32, "type", "range");
		__builder.AddAttribute(33, "min", "0");
		__builder.AddAttribute(34, "max", "100");
		__builder.AddAttribute(35, "value", BindConverter.FormatValue(MaxValue));
		__builder.AddAttribute(36, "oninput", EventCallback.Factory.CreateBinder(this, delegate(int __value)
		{
			MaxValue = __value;
		}, MaxValue));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		__builder.AddMarkupContent(37, "\n            ");
		__builder.OpenElement(38, "span");
		__builder.AddAttribute(39, "class", "range-value");
		__builder.AddContent(40, MaxValue);
		__builder.AddContent(41, "%");
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.AddMarkupContent(42, "\n    ");
		__builder.OpenElement(43, "div");
		__builder.AddAttribute(44, "class", "range-meta");
		__builder.OpenElement(45, "span");
		__builder.AddContent(46, "Selected: ");
		__builder.AddContent(47, MaxValue - MinValue);
		__builder.AddContent(48, "%");
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.CloseElement();
	}

	protected override void OnParametersSet()
	{
		if (MinValue > MaxValue)
		{
			int minValue = MinValue;
			MinValue = MaxValue;
			MaxValue = minValue;
		}
	}

	private Task OnMinChange()
	{
		if (MinValue > MaxValue)
		{
			MinValue = MaxValue;
		}
		return MinValueChanged.InvokeAsync(MinValue);
	}

	private Task OnMaxChange()
	{
		if (MaxValue < MinValue)
		{
			MaxValue = MinValue;
		}
		return MaxValueChanged.InvokeAsync(MaxValue);
	}
}
