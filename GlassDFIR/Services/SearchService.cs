using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace GlassDFIR.Services
{
    public class SearchService : ISearchService
    {
        // For MVP, we might search across all loaded CSVs or a specific index. 
        // Since we don't have a central index yet, we'll implement a stub that requires
        // future integration with the output directory.
        
        private readonly ICaseManager _caseManager;
        private const int ContextLength = 40;

        public SearchService(ICaseManager caseManager)
        {
            _caseManager = caseManager;
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<SearchResult>();

            return await Task.Run(() =>
            {
                var results = new List<SearchResult>();
                string outputDir = _caseManager.GetCurrentOutputPath();
                if (!Directory.Exists(outputDir)) return results;

                var csvFiles = Directory.GetFiles(outputDir, "*.csv", SearchOption.TopDirectoryOnly);
                
                // Try to compile regex, fallback to literal if invalid
                Regex searchRegex;
                try 
                {
                    searchRegex = new Regex(query, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                catch 
                {
                    searchRegex = new Regex(Regex.Escape(query), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }

                foreach (var file in csvFiles)
                {
                    try 
                    {
                        var fileName = Path.GetFileName(file);
                        string toolName = DetermineToolNameFromFileName(fileName);

                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream))
                        {
                            int lineNum = 0;
                            string? line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                lineNum++;
                                var match = searchRegex.Match(line);
                                if (match.Success)
                                {
                                    int matchStart = match.Index;
                                    int matchEnd = match.Index + match.Length;

                                    int contextStart = Math.Max(0, matchStart - ContextLength);
                                    int contextEnd = Math.Min(line.Length, matchEnd + ContextLength);

                                    results.Add(new SearchResult
                                    {
                                        Source = fileName,
                                        ToolName = toolName,
                                        Content = line,
                                        MatchContextBefore = line.Substring(contextStart, matchStart - contextStart),
                                        MatchContextAfter = line.Substring(matchEnd, contextEnd - matchEnd),
                                        LineReference = $"Line {lineNum}"
                                    });

                                    if (results.Count > 5000) return results; // Slightly higher cap for master search
                                }
                            }
                        }
                    }
                    catch { /* Ignore read errors */ }
                }
                return results;
            });
        }

        private string DetermineToolNameFromFileName(string fileName)
        {
            // Try to map back to tool names used in MainViewModel
            if (fileName.StartsWith("Volatility_"))
            {
                // Volatility_pslist_20260210_163144.csv -> Volatility - pslist
                var parts = fileName.Split('_');
                if (parts.Length >= 2) return $"Volatility - {parts[1]}";
            }
            
            if (fileName.StartsWith("MFTECmd_")) return "MFTECmd";
            if (fileName.StartsWith("EvtxECmd_")) return "EvtxECmd";
            if (fileName.StartsWith("AmcacheParser_")) return "AmcacheParser";
            if (fileName.StartsWith("AppCompatCacheParser_")) return "AppCompatCacheParser";
            if (fileName.StartsWith("LECmd_")) return "LECmd";
            if (fileName.StartsWith("JLECmd_")) return "JLECmd";
            if (fileName.StartsWith("RBCmd_")) return "RBCmd";
            if (fileName.StartsWith("SrumECmd_")) return "SrumECmd";
            if (fileName.StartsWith("SumECmd_")) return "SumECmd";
            if (fileName.StartsWith("WxTCmd_")) return "WxTCmd";
            if (fileName.StartsWith("SBECmd_")) return "SBECmd";
            if (fileName.StartsWith("RECmd_")) return "RECmd";
            if (fileName.StartsWith("PECmd_")) return "PECmd";
            if (fileName.StartsWith("Hayabusa_")) return "Hayabusa";

            return "Unknown Source";
        }
    }
}
