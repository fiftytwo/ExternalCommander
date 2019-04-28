using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class IpcClientMemoryMappedFile : IpcClient
    {
        private readonly long _bufferCapacity;
        private readonly EventWaitHandle _clientReadyEvent;
        private readonly EventWaitHandle _serverReadyEvent;

        private bool _isDisposed;

        public IpcClientMemoryMappedFile ( string name,
            long bufferCapacity = MemoryMappedFileStream.DefaultCapacity )
        : base( name )
        {
            try
            {
                _bufferCapacity = bufferCapacity;
                _clientReadyEvent = new EventWaitHandle( false,
                    EventResetMode.AutoReset, name + "1c" );
                _serverReadyEvent = new EventWaitHandle( false,
                    EventResetMode.AutoReset, name + "0c" );
            }
            catch
            {
                _serverReadyEvent?.Dispose();
                _clientReadyEvent?.Dispose();
                throw;
            }
        }

        public override Stream Connect ()
        {
            _clientReadyEvent.Set();
            _serverReadyEvent.WaitOne();
            return new MemoryMappedFileStream( Name, false, _bufferCapacity, _bufferCapacity );
        }

        public override Task<Stream> ConnectAsync ( CancellationToken token = default( CancellationToken ) )
        {
            if( token.IsCancellationRequested )
                return Task.FromCanceled<Stream>( token );

            _clientReadyEvent.Set();
            
            var task = _serverReadyEvent.WaitAsync( Timeout.Infinite, token )
                .ContinueWith<Stream>( antecedent => antecedent.Result
                    ? new MemoryMappedFileStream( Name, false, _bufferCapacity, _bufferCapacity )
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
                _serverReadyEvent?.Dispose();
                _clientReadyEvent?.Dispose();
            }

            _isDisposed = true;

            base.Dispose( disposing );
        }
    }
}