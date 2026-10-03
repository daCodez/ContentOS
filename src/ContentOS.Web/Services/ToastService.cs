using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace ContentOS.Web.Services;

public class ToastService : IAsyncDisposable
{
	private readonly IJSRuntime _jsRuntime;

	private IJSObjectReference? _module;

	public ToastService(IJSRuntime jsRuntime)
	{
		_jsRuntime = jsRuntime;
	}

	public async Task ShowToast(string variant, string title, string? description = null)
	{
		if (_module == null)
		{
			_module = await JSRuntimeExtensions.InvokeAsync<IJSObjectReference>(_jsRuntime, "import", new object[1] { "./slatekit.js" });
		}
		await _jsRuntime.InvokeVoidAsync("contentOsInterop.showToast", variant, title, description ?? string.Empty);
	}

	public async ValueTask DisposeAsync()
	{
		if (_module != null)
		{
			await _module.DisposeAsync();
		}
	}
}
