using System.Windows;
using GlassDFIR.Services;
using GlassDFIR.ViewModels;
using GlassDFIR.Views;
using Microsoft.Extensions.DependencyInjection;

using System.Windows.Threading;

namespace GlassDFIR
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IToolManager, ToolManager>();
            services.AddSingleton<IDownloaderService, DownloaderService>();
            services.AddSingleton<ICsvParsingService, CsvParsingService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<IAsnLookupService, AsnLookupService>();
            services.AddSingleton<ICaseManager, CaseManager>();
            services.AddSingleton<IVssService, VssService>();
            services.AddSingleton<IEvidenceCollectionService, EvidenceCollectionService>();
            services.AddSingleton<IBrowserHistoryService, BrowserHistoryService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<RapidSearchViewModel>();
            services.AddTransient<TimelineViewModel>();
            services.AddTransient<CaseManagementViewModel>();

            // Views
            services.AddTransient<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true; // Try to prevent crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception);
        }

        private void LogException(Exception? ex)
        {
            if (ex == null) return;
            string logPath = "crash.log";
            string message = $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n\n";
            try
            {
                System.IO.File.AppendAllText(logPath, message);
                MessageBox.Show($"Application Error: {ex.Message}\nCheck crash.log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* Best effort */ }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
