using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public abstract class IpcClient : IDisposable
    {
        protected string Name;
        
        protected IpcClient ( string name )
        {
            Name = name;
        }

        public static IpcClient Create ( string name )
        {
            var platformId = Environment.OSVersion.Platform;
            if( platformId == PlatformID.Unix || platformId == PlatformID.MacOSX )
                return new IpcClientUnixDomainSocket( name );
            
            return new IpcClientMemoryMappedFile( name );
        }

        public abstract Stream Connect ();
        public abstract Task<Stream> ConnectAsync ( CancellationToken token = default( CancellationToken ) );

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
