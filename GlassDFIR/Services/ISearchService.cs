using System.Collections.Generic;
using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public interface ISearchService
    {
        Task<IEnumerable<SearchResult>> SearchAsync(string query);
    }

    public class SearchResult
    {
        public string Source { get; set; } = string.Empty; // Filename
        public string ToolName { get; set; } = string.Empty; // e.g. "Volatility - pslist"
        public string Content { get; set; } = string.Empty; // The matching line
        public string MatchContextBefore { get; set; } = string.Empty;
        public string MatchContextAfter { get; set; } = string.Empty;
        public string LineReference { get; set; } = string.Empty;
    }
}
