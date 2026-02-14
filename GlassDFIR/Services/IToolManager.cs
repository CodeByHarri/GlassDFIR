using System.Collections.Generic;
using System.Threading.Tasks;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public interface IToolManager
    {
        IEnumerable<ToolModule> GetAvailableTools();
        Task ScanForToolsAsync();
        bool IsToolAvailable(string toolName);
        bool IsEzToolsInstalled();
        bool IsHayabusaInstalled();
        bool IsVolatilityInstalled();
    }
}
