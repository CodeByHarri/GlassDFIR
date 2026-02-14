using System.Data;
using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public interface ICsvParsingService
    {
        Task<DataTable> ParseCsvToDataTableAsync(string filePath);
    }
}
