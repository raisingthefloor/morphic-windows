using System.Threading.Tasks;

namespace Morphic.InstallerService.Contracts
{
    public interface IInstallerService
    {
        Task Install(Package package);
        Task Uninstall(Package package);
    }
}
