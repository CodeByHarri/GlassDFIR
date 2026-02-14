using System.Data;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace GlassDFIR.Services
{
    public class CsvParsingService : ICsvParsingService
    {
        public async Task<DataTable> ParseCsvToDataTableAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var dt = new DataTable();
                if (!File.Exists(filePath)) return dt;

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    PrepareHeaderForMatch = args => args.Header.ToLower(),
                    BufferSize = 4096 * 10, // Increase buffer for large forensic CSVs
                };

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                using (var csv = new CsvReader(reader, config))
                {
                    if (!csv.Read()) return dt;
                    csv.ReadHeader();
                    var headers = csv.HeaderRecord;

                    if (headers == null) return dt;

                    // 1. Add columns with duplicate handling (Zimmerman tools like EvtxECmd/WxTCmd often have duplicates)
                    foreach (var header in headers)
                    {
                        string originalName = string.IsNullOrWhiteSpace(header) ? "Column" : header;
                        string colName = originalName.ToLower(); 
                        
                        int count = 1;
                        while (dt.Columns.Contains(colName))
                        {
                            colName = $"{originalName.ToLower()}_{count++}";
                        }
                        dt.Columns.Add(colName, typeof(string)); 
                    }

                    // 2. Read rows with optimization
                    dt.BeginLoadData();
                    try
                    {
                        while (csv.Read())
                        {
                            var row = dt.NewRow();
                            for (int i = 0; i < headers.Length; i++)
                            {
                                if (i < dt.Columns.Count)
                                {
                                    row[i] = csv.GetField(i);
                                }
                            }
                            dt.Rows.Add(row);
                        }
                    }
                    finally
                    {
                        dt.EndLoadData();
                    }
                }
                return dt;
            });
        }
    }
}
