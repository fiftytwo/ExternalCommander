using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class TestIpcServer : TestServer
    {
        private const string IpcChannelName = "com.fiftytwo.ExternalCommander";

        protected override async Task ProcessRemoteCommands ( CancellationToken token )
        {
            Logger.Log( "ProcessRemoteCommands()" );

            using( var server = new IpcServerMemoryMappedFile( IpcChannelName ) )
            {
                Logger.Log( GetType() + " created" );

                while ( !token.IsCancellationRequested )
                {
                    try
                    {
                        Logger.Log( "Waiting for connection..." );
                        using( var stream = await server.AcceptAsync( token ) )
                        {
                            Logger.Log( "Connected. Receiving command string..." );
                            var command = await stream.ReceiveStringAsync( token );

                            var result = ExecuteCommand( command );

                            Logger.Log( "Sending result..." );
                            await stream.SendStringAsync( result, token );
                        }
                    }
                    catch( OperationCanceledException )
                    {
                        Logger.Log( "Operation canceled." );
                        break;
                    }
                    catch( ObjectDisposedException )
                    {
                        Logger.Log( "Server disposed." );
                        break;
                    }
                    catch( Exception ex )
                    {
                        Logger.Log( "Connection error: " + ex );
                        break;
                    }
                }
                
                Logger.Log( "Processor stopped." );
            }
        }
    }
}
