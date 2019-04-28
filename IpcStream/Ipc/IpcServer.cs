using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace Fiftytwo
{
    public abstract class IpcServer : IDisposable
    {
        protected string Name;
        
        protected IpcServer ( string name )
        {
            Name = name;
        }

        public static IpcServer Create ( string name )
        {
            var platformId = Environment.OSVersion.Platform;
            if( platformId == PlatformID.Unix || platformId == PlatformID.MacOSX )
                return new IpcServerUnixDomainSocket( name );
            
            return new IpcServerMemoryMappedFile( name );
        }

        public abstract Stream Accept ();
        public abstract Task<Stream> AcceptAsync ( CancellationToken token = default( CancellationToken ) );

        protected virtual void Dispose ( bool disposing )
        {
        }

        public void Dispose ()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }
    }
}
