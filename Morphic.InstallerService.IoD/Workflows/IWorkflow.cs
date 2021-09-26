using Morphic.Core;
using System.Threading.Tasks;

namespace IoDCLI.Workflows
{
    public interface IWorkflow<TError> where TError : new()
    {
        Task<IMorphicResult<bool, TError>> Install();
        Task<IMorphicResult<bool, TError>> Uninstall();
    }
}
