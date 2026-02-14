using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public interface IDownloaderService
    {
        Task EnsureToolsAreDownloadedAsync();
        Task DownloadEzToolsAsync();
        Task DownloadHayabusaAsync();
        Task DownloadVolatilityAsync();
        Task DownloadWinPmemAsync();
        Task DownloadYaraRulesAsync();
        Task DownloadAsnDataAsync();
    }
}
