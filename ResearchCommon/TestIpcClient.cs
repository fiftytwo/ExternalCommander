using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class TestIpcClient : ITestClient
    {
        private const string IpcChannelName = "com.fiftytwo.ExternalCommander";

        public async Task<string> ExecuteRemoteCommand ( string command, CancellationToken token = default( CancellationToken ) )
        {
            Logger.Log( $"ExecuteRemoteCommand({command})" );
            
            string result = null;

            try
            {
                using( var client = new IpcClientMemoryMappedFile( IpcChannelName ) )
                {
                    Logger.Log( GetType() + " created. Connecting..." );
                    var stream = await client.ConnectAsync( token );

                    Logger.Log( "Connected. Sending command..." );
                    await stream.SendStringAsync( command, token );

                    Logger.Log( "Command sent. Receiving response..." );
                    result = await stream.ReceiveStringAsync( token );
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