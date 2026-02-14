using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Services;

namespace GlassDFIR.ViewModels
{
    public class SourceFilter
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public partial class SearchViewModel : BladeViewModel
    {
        private readonly ISearchService _searchService;
        private readonly MainViewModel _mainViewModel;
        private readonly ICsvParsingService _csvParsingService;
        private System.Threading.CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private int _resultCount;

        [ObservableProperty]
        private bool _searchPerformed;

        [ObservableProperty]
        private string _matchedSourcesSummary = string.Empty;

        [ObservableProperty]
        private string? _selectedSource;

        public ObservableCollection<SearchResult> Results { get; } = new ObservableCollection<SearchResult>();
        public System.ComponentModel.ICollectionView FilteredResults { get; }
        public ObservableCollection<SourceFilter> SourceFilters { get; } = new ObservableCollection<SourceFilter>();

        public SearchViewModel(ISearchService searchService, MainViewModel mainViewModel, ICsvParsingService csvParsingService)
        {
            _searchService = searchService;
            _mainViewModel = mainViewModel;
            _csvParsingService = csvParsingService;
            Title = "Master Search";
            Icon = "🔍";

            FilteredResults = System.Windows.Data.CollectionViewSource.GetDefaultView(Results);
            FilteredResults.Filter = FilterResults;
        }

        private bool FilterResults(object obj)
        {
            if (obj is SearchResult result)
            {
                if (string.IsNullOrEmpty(SelectedSource) || SelectedSource == "All Sources")
                    return true;
                return result.ToolName == SelectedSource;
            }
            return false;
        }

        partial void OnSelectedSourceChanged(string? value)
        {
            FilteredResults.Refresh();
        }

        partial void OnSearchQueryChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token); // Debounce
                    System.Windows.Application.Current.Dispatcher.Invoke(() => PerformSearchCommand.Execute(null));
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        [RelayCommand]
        private async Task PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Results.Clear();
                ResultCount = 0;
                SearchPerformed = false;
                SourceFilters.Clear();
                return;
            }

            IsSearching = true;
            SearchPerformed = true;
            try
            {
                var searchResults = await _searchService.SearchAsync(SearchQuery);
                var list = searchResults.ToList();
                
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    Results.Clear();
                    foreach (var result in list)
                    {
                        Results.Add(result);
                    }
                    ResultCount = Results.Count;
                    
                    RefreshFilters(list);
                    
                    var uniqueSources = list.Select(r => r.ToolName).Distinct().ToList();
                    MatchedSourcesSummary = uniqueSources.Any() 
                        ? "Results found in: " + string.Join(", ", uniqueSources) 
                        : string.Empty;

                    // Reset filter when new search starts unless we want to stay? 
                    // Usually better to reset to "All"
                    SelectedSource = "All Sources";
                });
            }
            finally
            {
                IsSearching = false;
            }
        }

        private void RefreshFilters(List<SearchResult> allResults)
        {
            SourceFilters.Clear();
            SourceFilters.Add(new SourceFilter { Name = "All Sources", Count = allResults.Count });

            var grouped = allResults.GroupBy(r => r.ToolName)
                                   .Select(g => new SourceFilter { Name = g.Key, Count = g.Count() })
                                   .OrderByDescending(f => f.Count);

            foreach (var filter in grouped)
            {
                SourceFilters.Add(filter);
            }
        }

        [RelayCommand]
        private void GoToResult(SearchResult result)
        {
            if (result == null) return;

            var categoryBlades = _mainViewModel.Blades.OfType<CategoryBladeViewModel>().ToList();
            
            TimelineViewModel? targetBlade = null;
            foreach (var category in categoryBlades)
            {
                targetBlade = category.SubBlades.OfType<TimelineViewModel>()
                    .FirstOrDefault(v => v.HeaderText.Contains(result.ToolName) || v.Title == result.ToolName);
                if (targetBlade != null) break;
            }

            if (targetBlade != null)
            {
                _mainViewModel.CurrentBlade = targetBlade;
                _ = targetBlade.LoadCsv(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlassDFIR_Output", result.Source));
            }
        }
    }
}
