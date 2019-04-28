using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public interface ITestClient
    {
        Task<string> ExecuteRemoteCommand ( string command,
            CancellationToken token = default( CancellationToken) );
    }
}
