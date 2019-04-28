using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class IpcClientUnixDomainSocket : IpcClient
    {
        private UnixDomainSocketEndPoint _endPoint;
        
        private bool _isDisposed;

        public IpcClientUnixDomainSocket ( string name ) : base( name )
        {
            _endPoint = new UnixDomainSocketEndPoint( name );
        }

        public override Stream Connect ()
        {
            var client = new Socket( AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified );
            client.Connect( _endPoint );
            return new NetworkStream( client, true );
        }

        public override Task<Stream> ConnectAsync ( CancellationToken token = default( CancellationToken ) )
        {
            var client = new Socket( AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified );

            if (token.IsCancellationRequested)
                throw new TaskCanceledException();

            var cancellationHelper = new CancellationHelper<Socket>( client, client.Close,
                token.CanBeCanceled ? token : CancellationToken.None );

            var task = Task.Factory.FromAsync(
                ( callback, state ) => client.BeginConnect( _endPoint, callback, state ),
                ( iar ) =>
                {
                    var helper = ( CancellationHelper<Socket> )iar.AsyncState;
                    if( token.IsCancellationRequested )
                    {
                        throw new OperationCanceledException();
                    }

                    helper.Source.EndConnect( iar );

                    helper.SetOperationCompleted();
                    
                    return ( Stream )new NetworkStream( helper.Source, true );
                },
                cancellationHelper );

            return task;
        }

        protected override void Dispose ( bool disposing )
        {
            if( _isDisposed )
                return;

            _isDisposed = true;

            base.Dispose( disposing );
        }
    }
}
