using System.Collections.Generic;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public interface IBrowserHistoryService
    {
        List<BrowserHistoryItem> GetHistory(string historyFilePath, BrowserType browserType, string userProfileName);
        void ExportToCsv(List<BrowserHistoryItem> items, string outputPath);
    }
}
