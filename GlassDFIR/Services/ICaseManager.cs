using System.Collections.Generic;
using System.Threading.Tasks;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public interface ICaseManager
    {
        CaseModel? CurrentCase { get; }
        event Action? CaseChanged;
        
        Task InitializeAsync();
        IEnumerable<CaseModel> GetCases();
        Task<CaseModel> CreateCaseAsync();
        Task ActivateCaseAsync(CaseModel? @case);
        Task DeleteCaseAsync(CaseModel @case);
        
        string GetCurrentOutputPath();
        string GetGlobalOutputPath();
    }
}
