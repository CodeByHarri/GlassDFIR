using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Models;
using GlassDFIR.Services;

namespace GlassDFIR.ViewModels
{
    public partial class DashboardViewModel : BladeViewModel
    {
        private readonly IToolManager _toolManager;
        private readonly MainViewModel _mainViewModel;
        private readonly ICsvParsingService _csvParsingService;
        private readonly IDownloaderService _downloaderService;
        private readonly ICaseManager _caseManager;

        private readonly IVssService _vssService;
        private readonly IBrowserHistoryService _browserHistoryService;

        public ObservableCollection<ToolModule> Tools { get; } = new ObservableCollection<ToolModule>();

        [ObservableProperty]
        private bool _ezToolsInstalled;

        [ObservableProperty]
        private bool _hayabusaInstalled;

        [ObservableProperty]
        private bool _isEzDownloading;

        [ObservableProperty]
        private bool _isEzInstalling;

        [ObservableProperty]
        private bool _isHayabusaDownloading;

        [ObservableProperty]
        private bool _isHayabusaInstalling;

        [ObservableProperty]
        private bool _volatilityInstalled;

        [ObservableProperty]
        private bool _isVolatilityDownloading;

        [ObservableProperty]
        private bool _isVolatilityInstalling;

        public IAsyncRelayCommand RunToolCommand { get; }

        public DashboardViewModel(IToolManager toolManager, MainViewModel mainViewModel, ICsvParsingService csvParsingService, IDownloaderService downloaderService, ICaseManager caseManager, IVssService vssService, IBrowserHistoryService browserHistoryService)
        {
            _toolManager = toolManager;
            _mainViewModel = mainViewModel;
            _csvParsingService = csvParsingService;
            _downloaderService = downloaderService;
            _caseManager = caseManager;
            _vssService = vssService;
            _browserHistoryService = browserHistoryService;
            Title = "Overview";
            Icon = "🏠";
            
            RunToolCommand = new AsyncRelayCommand<ToolModule>(RunTool);
            
            // Check tools on load
            // Using Execute ensures the command logic runs (including checking CanExecute if applicable)
            // Since it's async void (fire and forget), we just trigger it.
            // Start auto-refresh loop
            StartAutoRefresh();
        }

        [RelayCommand]
        private async Task CreateNewCase()
        {
            var dashCat = _mainViewModel.Blades.OfType<CategoryBladeViewModel>().FirstOrDefault(c => c.Title == "Dashboard");
            if (dashCat != null)
            {
                var caseMgmt = dashCat.SubBlades.OfType<CaseManagementViewModel>().FirstOrDefault();
                if (caseMgmt != null)
                {
                    _mainViewModel.CurrentBlade = caseMgmt;
                    await caseMgmt.CreateNewCase();
                }
            }
        }

        private async void StartAutoRefresh()
        {
            while (true)
            {
                await Refresh();
                await Task.Delay(5000); // Refresh every 5 seconds
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            var ezInstalled = _toolManager.IsEzToolsInstalled();
            var hayabusaInstalled = _toolManager.IsHayabusaInstalled();

            // Update scalar properties on UI thread (marshalling happens automatically for simple props but explicit is safer if needed)
            // Actually, for ObservableProperty generated code, standard INPC marshalling applies.
            // But usually safer to just set them.
            EzToolsInstalled = ezInstalled;
            HayabusaInstalled = hayabusaInstalled;
            VolatilityInstalled = _toolManager.IsVolatilityInstalled();

            await _toolManager.ScanForToolsAsync();
            
            // Update ObservableCollection ON UI THREAD
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                Tools.Clear();
                foreach (var tool in _toolManager.GetAvailableTools())
                {
                    Tools.Add(tool);
                }
            });
        }

        [RelayCommand]
        private async Task DownloadEzTools()
        {
            IsEzDownloading = true;
            IsEzInstalling = true;

            // Start the download/install process but don't await immediately
            var installTask = _downloaderService.DownloadEzToolsAsync();

            // Poll while the task is running to catch tools as they appear
            while (!installTask.IsCompleted)
            {
                await Task.Delay(1000); // Fast updates (1s) for "real-time" feel
                await Refresh();
            }

            // Ensure we catch any exceptions and finalize state
            try
            {
                await installTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EZ Install Failed: {ex.Message}");
            }

            IsEzDownloading = false;
            IsEzInstalling = false;
            await Refresh();
        }

        [RelayCommand]
        private async Task DownloadHayabusa()
        {
            IsHayabusaDownloading = true;
            IsHayabusaInstalling = true;

            var installTask = _downloaderService.DownloadHayabusaAsync();

            while (!installTask.IsCompleted)
            {
                await Task.Delay(1000);
                await Refresh();
            }

            try
            {
                await installTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hayabusa Install Failed: {ex.Message}");
            }

            IsHayabusaDownloading = false;
            IsHayabusaInstalling = false;
            await Refresh();
        }

        [RelayCommand]
        private async Task DownloadVolatility()
        {
            IsVolatilityDownloading = true;
            IsVolatilityInstalling = true;

            var volTask = _downloaderService.DownloadVolatilityAsync();
            var winpmemTask = _downloaderService.DownloadWinPmemAsync();
            var yaraTask = _downloaderService.DownloadYaraRulesAsync();

            while (!volTask.IsCompleted || !winpmemTask.IsCompleted || !yaraTask.IsCompleted)
            {
                await Task.Delay(1000);
                await Refresh();
            }

            try
            {
                await volTask;
                await winpmemTask;
                await yaraTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Volatility Install Failed: {ex.Message}");
            }

            IsVolatilityDownloading = false;
            IsVolatilityInstalling = false;
            await Refresh();
        }

        private async Task RunTool(ToolModule? tool) // Allow null matches signature
        {
            if (tool == null) return;
            await Task.CompletedTask;
            // Navigate to ToolRunView via OpenBlade so it shows in the sidebar
            _mainViewModel.OpenBlade(new ToolRunnerViewModel(tool, _csvParsingService, _mainViewModel, _toolManager, _caseManager, _vssService, _browserHistoryService));
        }

        [RelayCommand]
        private void RunAll()
        {
            System.Diagnostics.Debug.WriteLine("Running all tools...");
        }
    }
}
