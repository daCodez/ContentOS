using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SlateKit;

public class SkeletonLoader : ComponentBase
{
	private string _skeletonClass = "skeleton";

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	[Parameter]
	public bool Active { get; set; } = true;

	[Parameter]
	public int Height { get; set; } = 16;

	[Parameter]
	public int Width { get; set; } = 100;

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", _skeletonClass);
		__builder.AddContent(2, ChildContent);
		__builder.CloseElement();
	}

	protected override void OnParametersSet()
	{
		UpdateClass();
	}

	private void UpdateClass()
	{
		List<string> list = new List<string> { "skeleton" };
		if (!Active)
		{
			list.Add("skeleton-hidden");
		}
		_skeletonClass = string.Join(" ", list);
		StateHasChanged();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
	}

	public void Dispose()
	{
	}
}
