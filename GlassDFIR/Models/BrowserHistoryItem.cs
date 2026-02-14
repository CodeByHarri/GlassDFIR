using System;

namespace GlassDFIR.Models
{
    public class BrowserHistoryItem
    {
        [CsvHelper.Configuration.Attributes.Name("URL")]
        public string Url { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Title")]
        public string Title { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Visit Time (UTC)")]
        public DateTime VisitTime { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Visit Count")]
        public int VisitCount { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Browser")]
        public string Browser { get; set; } = string.Empty; // Source: "Chrome", "Edge", "Firefox"

        [CsvHelper.Configuration.Attributes.Name("User Profile")]
        public string UserProfile { get; set; } = string.Empty;
    }

    public enum BrowserType
    {
        Chrome,
        Edge,
        Firefox
    }
}
