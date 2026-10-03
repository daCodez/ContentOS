using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SlateKit;

public class DatePicker : ComponentBase
{
	private ElementReference? anchorElement;

	private ElementReference? triggerElement;

	private ElementReference? calendarElement;

	private string _calendarClass = "calendar";

	private string _placeholderClass = "placeholder";

	[Parameter]
	public DateTime? SelectedDate { get; set; }

	[Parameter]
	public EventCallback<DateTime?> SelectedDateChanged { get; set; }

	[Parameter]
	public string? Placeholder { get; set; } = "Pick a date";

	[Parameter]
	public TimeSpan? SelectedTime { get; set; } = new TimeSpan(9, 0, 0);

	private string SelectedDateString => SelectedDate.HasValue ? SelectedDate.Value.ToString("MMMM d, yyyy") : (Placeholder ?? "Pick a date");

	[Inject]
	private IJSRuntime JS { get; set; } = null;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "datepicker-anchor");
		__builder.AddElementReferenceCapture(2, delegate(ElementReference __value)
		{
			anchorElement = __value;
		});
		__builder.OpenElement(3, "button");
		__builder.AddAttribute(4, "class", "datepicker-trigger");
		__builder.AddAttribute(5, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Func<Task>)ToggleCalendar));
		__builder.AddElementReferenceCapture(6, delegate(ElementReference __value)
		{
			triggerElement = __value;
		});
		__builder.AddMarkupContent(7, "<svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\" ry=\"2\"></rect>\n            <line x1=\"16\" y1=\"2\" x2=\"16\" y2=\"6\"></line>\n            <line x1=\"8\" y1=\"2\" x2=\"8\" y2=\"6\"></line>\n            <line x1=\"3\" y1=\"10\" x2=\"21\" y2=\"10\"></line></svg>\n        ");
		__builder.OpenElement(8, "span");
		__builder.AddAttribute(9, "class", "datepicker-value " + _placeholderClass);
		__builder.AddContent(10, SelectedDateString);
		__builder.CloseElement();
		__builder.CloseElement();
		__builder.AddMarkupContent(11, "\n    ");
		__builder.OpenElement(12, "div");
		__builder.AddAttribute(13, "class", _calendarClass);
		__builder.AddEventStopPropagationAttribute(14, "onclick", value: true);
		__builder.AddElementReferenceCapture(15, delegate(ElementReference __value)
		{
			calendarElement = __value;
		});
		__builder.CloseElement();
		__builder.CloseElement();
	}

	[JSInvokable]
	public void OpenCalendar()
	{
		_calendarClass = "calendar open";
		StateHasChanged();
	}

	[JSInvokable]
	public void CloseCalendar()
	{
		_calendarClass = "calendar";
		StateHasChanged();
	}

	private async Task ToggleCalendar()
	{
		try
		{
			await JS.InvokeVoidAsync("contentOsInterop.toggleCalendar", triggerElement, calendarElement, DotNetObjectReference.Create(this));
		}
		catch
		{
		}
	}

	[JSInvokable]
	public void ConfirmDate(string dateString)
	{
		if (DateTime.TryParse(dateString, out var result))
		{
			SelectedDate = result;
			SelectedDateChanged.InvokeAsync(SelectedDate);
		}
		CloseCalendar();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			try
			{
				await JS.InvokeVoidAsync("contentOsInterop.setupCalendarClickOutside", DotNetObjectReference.Create(this));
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
