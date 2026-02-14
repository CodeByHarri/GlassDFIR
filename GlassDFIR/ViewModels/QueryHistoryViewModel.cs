using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Services;

namespace GlassDFIR.ViewModels
{
    public class QueryHistoryItem
    {
        public string Browser { get; set; } = string.Empty;
        public string UserProfile { get; set; } = string.Empty;
        public DateTime VisitTime { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public string FullUrl { get; set; } = string.Empty;
    }

    public class DomainFilter
    {
        public string Domain { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public partial class QueryHistoryViewModel : BladeViewModel
    {
        private readonly ICsvParsingService _csvParsingService;
        private readonly ICaseManager _caseManager;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        private string _filterText = "";

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged(nameof(FilterText));
                }
            }
        }

        [ObservableProperty]
        private string? _selectedDomain;

        public ObservableCollection<QueryHistoryItem> AllQueries { get; } = new();
        [ObservableProperty]
        private ObservableCollection<QueryHistoryItem> _filteredQueries = new();
        public ObservableCollection<DomainFilter> DomainFilters { get; } = new();

        public QueryHistoryViewModel(ICsvParsingService csvParsingService, ICaseManager caseManager)
        {
            _csvParsingService = csvParsingService;
            _caseManager = caseManager;

            Title = "Query History";
            Icon = "🔎"; 
        }

        partial void OnSelectedDomainChanged(string? value)
        {
            ApplyDomainFilter();
        }

        private void ApplyDomainFilter()
        {
            var newCollection = new ObservableCollection<QueryHistoryItem>();
            
            if (string.IsNullOrEmpty(SelectedDomain) || SelectedDomain == "All Domains")
            {
                foreach (var item in AllQueries) newCollection.Add(item);
            }
            else
            {
                var filtered = AllQueries.Where(x => x.Domain.Equals(SelectedDomain, StringComparison.OrdinalIgnoreCase));
                foreach (var item in filtered) newCollection.Add(item);
            }
            FilteredQueries = newCollection;
        }

        [RelayCommand]
        public async Task LoadHistory()
        {
            IsBusy = true;
            StatusMessage = "Scanning for Browser History...";
            AllQueries.Clear();
            DomainFilters.Clear();

            try
            {
                var outputDir = _caseManager.GetCurrentOutputPath();
                if (!Directory.Exists(outputDir))
                {
                    StatusMessage = "No output directory found.";
                    return;
                }

                var files = Directory.GetFiles(outputDir, "BrowserHistory_*.csv");
                if (!files.Any())
                {
                    StatusMessage = "No Browser History CSVs found. Run the tool first.";
                    return;
                }

                // Load the latest file
                var latestFile = files.OrderByDescending(f => f).First();
                StatusMessage = $"Parsing {Path.GetFileName(latestFile)}...";

                var dt = await _csvParsingService.ParseCsvToDataTableAsync(latestFile);
                
                var parsedItems = new List<QueryHistoryItem>();

                await Task.Run(() =>
                {
                    // Map columns flexibly
                    string colUrl = GetColumnName(dt, "URL", "Url", "url");
                    string colTime = GetColumnName(dt, "Visit Time (UTC)", "VisitTime", "Visit Time", "visit_time");
                    string colBrowser = GetColumnName(dt, "Browser", "browser", "Source Browser");
                    string colProfile = GetColumnName(dt, "User Profile", "UserProfile", "user_profile");

                    foreach (DataRow row in dt.Rows)
                    {
                        string url = string.IsNullOrEmpty(colUrl) ? "" : row[colUrl]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(url)) continue;

                        string query = ExtractSearchQuery(url);
                        if (!string.IsNullOrEmpty(query))
                        {
                            string domain = GetDomain(url);
                            
                            // Parse time
                            DateTime visitTime = DateTime.MinValue;
                            if (!string.IsNullOrEmpty(colTime) && row[colTime] != DBNull.Value)
                            {
                                DateTime.TryParse(row[colTime].ToString(), out visitTime);
                            }

                            parsedItems.Add(new QueryHistoryItem
                            {
                                Browser = !string.IsNullOrEmpty(colBrowser) ? row[colBrowser]?.ToString() ?? "" : "",
                                UserProfile = !string.IsNullOrEmpty(colProfile) ? row[colProfile]?.ToString() ?? "" : "",
                                VisitTime = visitTime,
                                Domain = domain,
                                SearchQuery = query,
                                FullUrl = url
                            });
                        }
                    }
                });

                foreach (var item in parsedItems.OrderByDescending(x => x.VisitTime))
                {
                    AllQueries.Add(item);
                }

                // Build Domain Filters
                var groups = parsedItems.GroupBy(x => x.Domain)
                                      .Select(g => new DomainFilter { Domain = g.Key, Count = g.Count() })
                                      .OrderByDescending(x => x.Count);

                DomainFilters.Add(new DomainFilter { Domain = "All Domains", Count = parsedItems.Where(x => !string.IsNullOrEmpty(x.Domain)).Count() });
                foreach (var g in groups) DomainFilters.Add(g);

                SelectedDomain = "All Domains"; // This will trigger OnSelectedDomainChanged -> ApplyDomainFilter
                ApplyDomainFilter(); // Call explicitly just in case SelectedDomain didn't change (was already "All Domains")
                StatusMessage = $"Found {parsedItems.Count} search queries.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GetColumnName(DataTable dt, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (dt.Columns.Contains(candidate)) return candidate;
            }
            return string.Empty;
        }

        private string GetDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Unknown";
            }
        }

        private string ExtractSearchQuery(string url)
        {
            try
            {
                var uri = new Uri(url);
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                
                // Common search query parameters
                string[] searchParams = { "q", "query", "p", "text", "wd", "k", "keywords" };

                foreach (var param in searchParams)
                {
                    string? val = queryParams[param];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }
            }
            catch
            {
                // Invalid URL
            }
            return string.Empty;
        }
    }
}
