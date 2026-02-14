using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public class DownloaderService : IDownloaderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _toolsRoot;
        private readonly string _ezRoot;
        private readonly string _hayabusaRoot;
        private readonly string _volatilityRoot;

        public DownloaderService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GlassDFIR");
            
            _toolsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            _ezRoot = Path.Combine(_toolsRoot, "EZ");
            _hayabusaRoot = Path.Combine(_toolsRoot, "Hayabusa");
            _volatilityRoot = Path.Combine(_toolsRoot, "Volatility");
        }

        public async Task EnsureToolsAreDownloadedAsync()
        {
            await DownloadEzToolsAsync();
            await DownloadHayabusaAsync();
            await DownloadVolatilityAsync();
            await DownloadWinPmemAsync();
        }

        public async Task DownloadEzToolsAsync()
        {
            if (!Directory.Exists(_toolsRoot)) Directory.CreateDirectory(_toolsRoot);
            if (!Directory.Exists(_ezRoot)) Directory.CreateDirectory(_ezRoot);

            // URL: https://f001.backblazeb2.com/file/EricZimmermanTools/Get-ZimmermanTools.zip
            string ezZipPath = Path.Combine(_toolsRoot, "Get-ZimmermanTools.zip");
            
            // If already exists (and presumably extracted), we might skip or overwrite. 
            // For now, simpler check: if zip doesn't exist, download it.
            // Force download/extract cycle for now to ensure consistency
            // In a robust app, verify hash or check install status before re-downloading.
            // But if user clicked "Download", they imply they want to fix/install it.
            
            try 
            {
                if (File.Exists(ezZipPath)) File.Delete(ezZipPath);
                await DownloadFileAsync("https://f001.backblazeb2.com/file/EricZimmermanTools/Get-ZimmermanTools.zip", ezZipPath);
                
                // Offload extraction/execution to background thread
                await Task.Run(async () => 
                {
                    try 
                    {
                        // Clean EZ root first?
                        // if (Directory.Exists(_ezRoot)) Directory.Delete(_ezRoot, true);
                        // Directory.CreateDirectory(_ezRoot);
                        
                        System.IO.Compression.ZipFile.ExtractToDirectory(ezZipPath, _ezRoot, true); 
                        
                        string scriptPath = Path.Combine(_ezRoot, "Get-ZimmermanTools.ps1");
                        if (File.Exists(scriptPath))
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo()
                            {
                                FileName = "powershell.exe",
                                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = _ezRoot // CRITICAL: Run script in the EZ folder
                            };
                            
                            var process = System.Diagnostics.Process.Start(psi);
                            if (process != null)
                            {
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                    catch { /* Handle extraction/execution error */ }
                });
            }
            catch (Exception ex)
            { 
                 // Log or rethrow? For now, swallow to avoid crash, but maybe debug print
                 System.Diagnostics.Debug.WriteLine($"EZ Install Error: {ex.Message}");
            }
        }

        public async Task DownloadHayabusaAsync()
        {
            if (!Directory.Exists(_toolsRoot)) Directory.CreateDirectory(_toolsRoot);
            if (!Directory.Exists(_hayabusaRoot)) Directory.CreateDirectory(_hayabusaRoot);

            string hayabusaZipPath = Path.Combine(_toolsRoot, "hayabusa.zip");
            // Hardcoded version for MVP reliability
            string url = "https://github.com/Yamato-Security/hayabusa/releases/download/v2.17.0/hayabusa-2.17.0-win-x64.zip";

            if (File.Exists(hayabusaZipPath)) File.Delete(hayabusaZipPath);

            try
            {
                await DownloadFileAsync(url, hayabusaZipPath);
                
                // Offload extraction to background thread to prevent UI freeze
                await Task.Run(() => 
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(hayabusaZipPath, _hayabusaRoot, true);
                });
            }
            catch (Exception ex)
            { 
                System.Diagnostics.Debug.WriteLine($"Hayabusa Install Error: {ex.Message}");
            }
        }

        public async Task DownloadVolatilityAsync()
        {
            if (!Directory.Exists(_toolsRoot)) Directory.CreateDirectory(_toolsRoot);
            if (!Directory.Exists(_volatilityRoot)) Directory.CreateDirectory(_volatilityRoot);

            string volZipPath = Path.Combine(_toolsRoot, "volatility3.zip");
            string url = "https://github.com/volatilityfoundation/volatility3/releases/download/v2.27.0/volatility3-win-exes-2.27.0.zip";

            if (File.Exists(volZipPath)) File.Delete(volZipPath);

            try
            {
                await DownloadFileAsync(url, volZipPath);
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(volZipPath, _volatilityRoot, true);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Volatility Install Error: {ex.Message}");
            }
        }

        public async Task DownloadWinPmemAsync()
        {
            if (!Directory.Exists(_toolsRoot)) Directory.CreateDirectory(_toolsRoot);
            if (!Directory.Exists(_volatilityRoot)) Directory.CreateDirectory(_volatilityRoot);

            string winpmemPath = Path.Combine(_volatilityRoot, "winpmem.exe");
            string url = "https://github.com/Velocidex/WinPmem/releases/download/v4.0.rc1/winpmem_mini_x64_rc2.exe";

            try
            {
                await DownloadFileAsync(url, winpmemPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WinPmem Install Error: {ex.Message}");
            }
        }

        public async Task DownloadYaraRulesAsync()
        {
            if (!Directory.Exists(_toolsRoot)) Directory.CreateDirectory(_toolsRoot);
            
            string yaraRoot = Path.Combine(_toolsRoot, "Yara");
            if (!Directory.Exists(yaraRoot)) Directory.CreateDirectory(yaraRoot);

            string yaraZipPath = Path.Combine(_toolsRoot, "yara_rules.zip");
            string url = "https://github.com/Yara-Rules/rules/archive/refs/heads/master.zip";

            if (File.Exists(yaraZipPath)) File.Delete(yaraZipPath);

            try
            {
                await DownloadFileAsync(url, yaraZipPath);
                await Task.Run(() =>
                {
                    // Extract to the Yara folder
                    System.IO.Compression.ZipFile.ExtractToDirectory(yaraZipPath, yaraRoot, true);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YARA Rules Install Error: {ex.Message}");
            }
        }

        public async Task DownloadAsnDataAsync()
        {
            string asnDir = Path.Combine(_toolsRoot, "AsnData");
            if (!Directory.Exists(asnDir)) Directory.CreateDirectory(asnDir);

            string zipPath = Path.Combine(asnDir, "kusto-cidr-asn.csv.zip");
            string url = "https://firewalliplists.gypthecat.com/lists/kusto/kusto-cidr-asn.csv.zip";

            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                await DownloadFileAsync(url, zipPath);
                
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, asnDir, true);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ASN Data Download Error: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            try 
            {
                var data = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(destinationPath, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download failed for {url}: {ex.Message}");
                throw; // Rethrow so caller knows it failed
            }
        }
    }
}
