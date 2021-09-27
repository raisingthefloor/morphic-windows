using System.Threading.Tasks;

namespace Morphic.InstallerService.Contracts
{
    public interface IInstallerService
    {
        Task Install(Package package);
        Task Install(string application, string[] arguments);
        Task Uninstall(Package package);
        Task Uninstall(string application, string[] arguments);
    }
}
