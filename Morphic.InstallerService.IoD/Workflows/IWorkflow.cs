using Morphic.Core;
using System.Threading.Tasks;

namespace IoDCLI.Workflows
{
    public interface IWorkflow<TError> where TError : new()
    {
        Task<MorphicResult<bool, TError>> Install();
        Task<MorphicResult<bool, TError>> Uninstall();
    }
}
