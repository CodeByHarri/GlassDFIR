using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public interface IAsnLookupService
    {
        Task InitializeAsync();
        bool IsReady { get; }
        (string ASN, string Name)? GetAsnInfo(string ip);
    }
}
