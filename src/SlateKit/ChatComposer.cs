using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SlateKit;

public class ChatComposer : ComponentBase
{
	[Parameter]
	public string Placeholder { get; set; } = "Type a message...";

	[Parameter]
	public string Message { get; set; } = "";

	[Parameter]
	public EventCallback<string> OnSend { get; set; }

	[Parameter]
	public EventCallback OnAddClick { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "chat-composer");
		__builder.OpenElement(2, "button");
		__builder.AddAttribute(3, "class", "composer-icon-btn");
		__builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnAddClick));
		__builder.AddAttribute(5, "aria-label", "Add");
		__builder.AddMarkupContent(6, "<svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"12\" y1=\"5\" x2=\"12\" y2=\"19\"></line><line x1=\"5\" y1=\"12\" x2=\"19\" y2=\"12\"></line></svg>");
		__builder.CloseElement();
		__builder.AddMarkupContent(7, "\n    ");
		__builder.OpenElement(8, "input");
		__builder.AddAttribute(9, "type", "text");
		__builder.AddAttribute(10, "class", "composer-input");
		__builder.AddAttribute(11, "onkeypress", EventCallback.Factory.Create((object)this, (Func<KeyboardEventArgs, Task>)HandleKeyPress));
		__builder.AddAttribute(12, "placeholder", Placeholder);
		__builder.AddAttribute(13, "value", BindConverter.FormatValue(Message));
		__builder.AddAttribute(14, "onchange", EventCallback.Factory.CreateBinder(this, delegate(string? __value)
		{
			Message = __value;
		}, Message));
		__builder.SetUpdatesAttributeName("value");
		__builder.CloseElement();
		__builder.AddMarkupContent(15, "\n    ");
		__builder.OpenElement(16, "button");
		__builder.AddAttribute(17, "class", "composer-icon-btn send");
		__builder.AddAttribute(18, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Func<Task>)OnSendClick));
		__builder.AddAttribute(19, "aria-label", "Send");
		__builder.AddMarkupContent(20, "<svg class=\"icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><path d=\"M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z\"></path><path d=\"M19 10v2a7 7 0 0 1-14 0v-2\"></path><line x1=\"12\" y1=\"19\" x2=\"12\" y2=\"23\"></line><line x1=\"8\" y1=\"23\" x2=\"16\" y2=\"23\"></line></svg>");
		__builder.CloseElement();
		__builder.CloseElement();
	}

	private async Task HandleKeyPress(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			await OnSend.InvokeAsync(Message);
			Message = "";
		}
	}

	private async Task OnSendClick()
	{
		await OnSend.InvokeAsync(Message);
		Message = "";
	}
}
