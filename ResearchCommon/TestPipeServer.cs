using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class TestPipeServer : TestServer
    {
        private const string IpcChannelName = "/Users/dmitry/projects/FIFTYTWO/com.fiftytwo.ExternalCommander";

        protected override async Task ProcessRemoteCommands ( CancellationToken token )
        {
            Logger.Log( "ProcessRemoteCommands()" );

            using( var server = new NamedPipeServerStream(
                IpcChannelName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous ) )
            {
                Logger.Log( GetType() + " created" );

                while( !token.IsCancellationRequested )
                {
                    try
                    {
                        Logger.Log( "Waiting for connection..." );
                        await server.WaitForConnectionAsync( token );

                        Logger.Log( "Connected. Receiving command string..." );
                        var command = await server.ReceiveStringAsync( token );

                        var result = ExecuteCommand( command );

                        Logger.Log( "Sending result..." );
                        await server.SendStringAsync( result, token );

                        Logger.Log( "Response sent. Waiting for pipe drain..." );
                        server.WaitForPipeDrain();

                        Logger.Log( "Pipe drained. Disconnecting..." );
                        server.Disconnect();
                        Logger.Log( "Disconnected" );
                    }
                    catch( OperationCanceledException )
                    {
                        Logger.Log( "Operation canceled." );
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