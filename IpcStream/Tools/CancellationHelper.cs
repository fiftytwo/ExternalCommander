using System;
using System.Threading;

namespace Fiftytwo
{
    public class CancellationHelper<T>
    {
        private bool _isOperationCompleted;
        private Action _cancelAction;
        private CancellationTokenRegistration _cancellationRegistration;

        public CancellationHelper ( T source, Action cancelAction, CancellationToken token )
        {
            if ( !token.CanBeCanceled )
            {
                _isOperationCompleted = true;
                return;
            }

            Source = source;
            _cancelAction = cancelAction;

            if( token.IsCancellationRequested )
                Cancel();
            else
                _cancellationRegistration = token.Register( Cancel );
        }

        public T Source { get; }

        public void SetOperationCompleted()
        {
            if( _isOperationCompleted )
                return;
            _cancellationRegistration.Dispose();
            _cancelAction = null;
            _isOperationCompleted = true;
        }

        private void Cancel ()
        {
            _cancelAction?.Invoke();

            SetOperationCompleted();
        }
    }
}