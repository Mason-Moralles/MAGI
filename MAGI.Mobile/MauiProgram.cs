using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Application.Validators;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Platform;
using MAGI.Mobile.Platform.LocalCache;
using MAGI.Mobile.Presentation.Pages;
using Microsoft.Extensions.Logging;

namespace MAGI.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		SQLitePCL.Batteries_V2.Init();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<HttpClient>();
		builder.Services.AddSingleton<ILocalCacheService, MauiLocalCacheService>();
		builder.Services.AddSingleton<IConnectivityService, MauiConnectivityService>();
		builder.Services.AddSingleton<ISettingsStore, MauiSettingsStore>();
		builder.Services.AddSingleton<IShareService, MauiShareService>();
		builder.Services.AddSingleton<AppState>();

		builder.Services.AddSingleton<GatewaySettingsValidator>();
		builder.Services.AddSingleton<ScheduleSlotValidator>();

		builder.Services.AddSingleton<GatewayApiClient>();
		builder.Services.AddSingleton<HealthApi>();
		builder.Services.AddSingleton<ChannelApi>();
		builder.Services.AddSingleton<ServiceApi>();
		builder.Services.AddSingleton<ScheduleApi>();
		builder.Services.AddSingleton<ImageApi>();

		builder.Services.AddSingleton<IChannelService, ChannelService>();
		builder.Services.AddSingleton<IDashboardService, DashboardService>();
		builder.Services.AddSingleton<IServiceControlService, ServiceControlService>();
		builder.Services.AddSingleton<IScheduleService, ScheduleService>();
		builder.Services.AddSingleton<IImageService, ImageService>();

		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ServicesViewModel>();
		builder.Services.AddTransient<ScheduleViewModel>();
		builder.Services.AddTransient<GalleryViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ServicesPage>();
		builder.Services.AddTransient<SchedulePage>();
		builder.Services.AddTransient<GalleryPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
