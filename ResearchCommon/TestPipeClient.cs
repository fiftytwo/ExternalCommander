using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class TestPipeClient : ITestClient
    {
        private const string IpcChannelName = "/Users/dmitry/projects/FIFTYTWO/com.fiftytwo.ExternalCommander";

        public async Task<string> ExecuteRemoteCommand ( string command,
            CancellationToken token = default( CancellationToken) )
        {
            Logger.Log( $"ExecuteRemoteCommand({command})" );
            
            string result = null;

            try
            {
                using( var client = new NamedPipeClientStream(
                    ".", IpcChannelName, PipeDirection.InOut, PipeOptions.Asynchronous ) )
                {
                    Logger.Log( GetType() + " created. Connecting..." );
                    await client.ConnectAsync( token );

                    Logger.Log( "Connected. Sending command..." );
                    await client.SendStringAsync( command, token );

                    Logger.Log( "Command sent. Receiving response..." );
                    result = await client.ReceiveStringAsync( token );
                    Logger.Log( "Response received." );
                }
            }
            catch( Exception ex )
            {
                Logger.Log( "Connection error: " + ex );
            }

            return result;
        }
    }
}
