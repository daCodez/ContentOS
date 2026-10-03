using System;
using Microsoft.Extensions.DependencyInjection;

namespace ContentOS.Infrastructure.Research.SeoData;

public static class SeoDataServiceExtensions
{
	public static IServiceCollection AddSeoDataProviders(this IServiceCollection services)
	{
		services.AddTransient<GoogleTrendsProvider>();
		services.AddTransient((Func<IServiceProvider, ISeoDataProvider>)((IServiceProvider sp) => sp.GetRequiredService<GoogleTrendsProvider>()));
		services.AddTransient<AhrefsProvider>();
		services.AddTransient<SemrushProvider>();
		services.AddTransient<GoogleKeywordPlannerProvider>();
		services.AddTransient<SearchConsoleProvider>();
		services.AddTransient<SeoDataAggregator>();
		return services;
	}
}
