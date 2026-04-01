using MAGI.Mobile.Presentation.Pages;

namespace MAGI.Mobile;

public partial class AppShell : Shell
{
	private readonly IServiceProvider _serviceProvider;

	public AppShell(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
		BuildNavigation();
	}

	private void BuildNavigation()
	{
		Items.Clear();

		var tabBar = new TabBar();
		tabBar.Items.Add(CreateTab("Обзор", "dashboard", () => _serviceProvider.GetRequiredService<DashboardPage>()));
		tabBar.Items.Add(CreateTab("Сервисы", "services", () => _serviceProvider.GetRequiredService<ServicesPage>()));
		tabBar.Items.Add(CreateTab("Расписание", "schedule", () => _serviceProvider.GetRequiredService<SchedulePage>()));
		tabBar.Items.Add(CreateTab("Галерея", "gallery", () => _serviceProvider.GetRequiredService<GalleryPage>()));
		tabBar.Items.Add(CreateTab("Настройки", "settings", () => _serviceProvider.GetRequiredService<SettingsPage>()));

		Items.Add(tabBar);
	}

	private static Tab CreateTab(string title, string route, Func<Page> pageFactory)
	{
		var shellContent = new ShellContent
		{
			Title = title,
			Route = route,
			ContentTemplate = new DataTemplate(pageFactory)
		};

		return new Tab
		{
			Title = title,
			Route = route,
			Items = { shellContent }
		};
	}
}
