using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class IpcServerMemoryMappedFile : IpcServer
    {
        private readonly long _bufferCapacity;
        private readonly EventWaitHandle _serverReadyEvent;
        private readonly EventWaitHandle _clientReadyEvent;

        private bool _isDisposed;

        public IpcServerMemoryMappedFile ( string name,
            long bufferCapacity = MemoryMappedFileStream.DefaultCapacity )
        : base( name )
        {
            try
            {
                _bufferCapacity = bufferCapacity;
                _serverReadyEvent = new EventWaitHandle( false,
                    EventResetMode.AutoReset, name + "0c" );
                _clientReadyEvent = new EventWaitHandle( false,
                    EventResetMode.AutoReset, name + "1c" );
            }
            catch
            {
                _clientReadyEvent?.Dispose();
                _serverReadyEvent?.Dispose();
                throw;
            }
        }

        public override Stream Accept ()
        {
            _serverReadyEvent.Set();
            _clientReadyEvent.WaitOne();
            return new MemoryMappedFileStream( Name, true, _bufferCapacity, _bufferCapacity );
        }

        public override Task<Stream> AcceptAsync ( CancellationToken token = default( CancellationToken ) )
        {
            if( token.IsCancellationRequested )
                return Task.FromCanceled<Stream>( token );

            _serverReadyEvent.Set();

            var task = _clientReadyEvent.WaitAsync( Timeout.Infinite, token )
                .ContinueWith<Stream>( antecedent => antecedent.Result
                    ? new MemoryMappedFileStream( Name, true, _bufferCapacity, _bufferCapacity )
                    : default( MemoryMappedFileStream ),
                    token );

            return task;
        }

        protected override void Dispose ( bool disposing )
        {
            if( _isDisposed )
                return;

            if( disposing )
            {
                _clientReadyEvent?.Dispose();
                _serverReadyEvent?.Dispose();
            }

            _isDisposed = true;
            
            base.Dispose( disposing );
        }
    }
}
