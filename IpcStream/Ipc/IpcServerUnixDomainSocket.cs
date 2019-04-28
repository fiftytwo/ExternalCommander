using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class IpcServerUnixDomainSocket : IpcServer
    {
        private Socket _server;

        private bool _isDisposed;
        
        public IpcServerUnixDomainSocket ( string name ) : base( name )
        {
            TryDeleteUnixDomainSocketFile();
            
            var endpoint = new UnixDomainSocketEndPoint( name );
            _server = new Socket( AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified );
            _server.Bind( endpoint );
            _server.Listen( 1 );
        }

        public override Stream Accept ()
        {
            var accepted = _server.Accept();
            return new NetworkStream( accepted, true );
        }

        public override Task<Stream> AcceptAsync ( CancellationToken token = default( CancellationToken ) )
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<Stream>( token );

            var cancellationHelper = new CancellationHelper<Socket>( _server, _server.Close,
                token.CanBeCanceled ? token : CancellationToken.None );

            var task = Task.Factory.FromAsync(
                _server.BeginAccept,
                ( iar ) =>
                {
                    var helper = ( CancellationHelper<Socket> )iar.AsyncState;
                    if( token.IsCancellationRequested )
                        throw new OperationCanceledException();

                    var accepted = helper.Source.EndAccept( iar );

                    helper.SetOperationCompleted();
                    
                    return ( Stream )new NetworkStream( accepted, true );
                },
                cancellationHelper );

            return task;
        }

        protected override void Dispose ( bool disposing )
        {
            if( _isDisposed )
                return;

            _server.Close();
            TryDeleteUnixDomainSocketFile();

            _isDisposed = true;

            base.Dispose( disposing );
        }

        private void TryDeleteUnixDomainSocketFile ()
        {
            try { File.Delete( Name ); }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }
    }
}
