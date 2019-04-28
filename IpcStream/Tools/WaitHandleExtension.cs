using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public static class WaitHandleExtension
    {
        public static Task<bool> WaitAsync ( this WaitHandle handle,
            int millisecondsTimeOutInterval = Timeout.Infinite,
            CancellationToken token = default( CancellationToken ) )
        {
            var tcs = new TaskCompletionSource<bool>();

            var cancellationHelper = new CancellationHelper<TaskCompletionSource<bool>>(
                null, () => tcs.TrySetCanceled( token ), token );

            var rwh = ThreadPool.RegisterWaitForSingleObject( handle, ( state, timedOut ) =>
            {
                if( !token.IsCancellationRequested )
                    tcs.TrySetResult( !timedOut );
            }, cancellationHelper, millisecondsTimeOutInterval, true );

            var t = tcs.Task;
            t.ContinueWith( antecedent =>
            {
                if( !antecedent.IsCanceled )
                    cancellationHelper.SetOperationCompleted();
                rwh.Unregister( null );
                return !antecedent.IsCanceled;
            }, CancellationToken.None );

            return t;
        }
    }
}
