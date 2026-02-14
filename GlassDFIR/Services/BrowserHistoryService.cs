using System;
using System.Collections.Generic;
using System.IO;
using GlassDFIR.Models;
using Microsoft.Data.Sqlite;

namespace GlassDFIR.Services
{
    public class BrowserHistoryService : IBrowserHistoryService
    {
        public List<BrowserHistoryItem> GetHistory(string historyFilePath, BrowserType browserType, string userProfileName)
        {
            var results = new List<BrowserHistoryItem>();

            if (!File.Exists(historyFilePath))
            {
                return results;
            }

            // Create a temporary copy to avoid locking issues if browser is open
            string tempPath = Path.Combine(Path.GetTempPath(), $"GlassDFIR_{Guid.NewGuid()}.tmp");
            try
            {
                File.Copy(historyFilePath, tempPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Error] Failed to copy history file: {ex.Message}");
                // If copy fails (e.g. strict locking), try to read directly (might fail if locked)
                tempPath = historyFilePath; 
            }

            try
            {
                using (var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly"))
                {
                    connection.Open();

                    string query = "";
                    if (browserType == BrowserType.Chrome || browserType == BrowserType.Edge)
                    {
                        // Chromium-based browsers share similar schema
                        // 'urls' table: id, url, title, visit_count, last_visit_time
                        // last_visit_time is Webkit timestamp (microseconds since 1601-01-01)
                        query = "SELECT url, title, visit_count, last_visit_time FROM urls";
                    }
                    else if (browserType == BrowserType.Firefox)
                    {
                        // Firefox 'moz_places' table
                        // url, title, visit_count, last_visit_date
                        // last_visit_date is PRTime (microseconds since 1970-01-01)
                        query = "SELECT url, title, visit_count, last_visit_date FROM moz_places";
                    }

                    using (var command = new SqliteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new BrowserHistoryItem
                            {
                                Browser = browserType.ToString(),
                                UserProfile = userProfileName,
                                Url = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                VisitCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                            };

                            long rawTime = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                            item.VisitTime = ConvertToDateTime(rawTime, browserType);

                            results.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[Error] Failed to parse history db: {ex.Message}");
            }
            finally
            {
                // Cleanup temp file
                if (tempPath != historyFilePath && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }

            return results;
        }

        private DateTime ConvertToDateTime(long rawTime, BrowserType type)
        {
            if (rawTime == 0) return DateTime.MinValue;

            try
            {
                if (type == BrowserType.Chrome || type == BrowserType.Edge)
                {
                    // Webkit: Microseconds since 1601-01-01
                    return DateTime.FromFileTimeUtc(rawTime * 10);
                }
                else if (type == BrowserType.Firefox)
                {
                    // PRTime: Microseconds since 1970-01-01
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return epoch.AddMicroseconds(rawTime);
                }
            }
            catch
            {
                return DateTime.MinValue;
            }
            return DateTime.MinValue;
        }

        public void ExportToCsv(List<BrowserHistoryItem> items, string outputPath)
        {
             // Simple CSV serialization
            try
            {
                using (var writer = new StreamWriter(outputPath))
                using (var csv = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture))
                {
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd HH:mm:ss" };
                    csv.WriteRecords(items);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Error] Failed to write CSV: {ex.Message}");
            }
        }
    }
}
