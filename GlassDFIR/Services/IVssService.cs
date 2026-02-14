using System.Threading.Tasks;

namespace GlassDFIR.Services
{
    public interface IVssService
    {
        string? CreateSnapshot(string volume);
        string? GetSnapshotVolume(string shadowId);
        string? CreateSymlinkToVolume(string devicePath);
        void CleanupSymlink(string symlinkPath);
        void DeleteSnapshot(string shadowId);
    }
}
