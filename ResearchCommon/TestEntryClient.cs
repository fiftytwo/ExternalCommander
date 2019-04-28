using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public static class TestEntryClient
    {
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static ITestClient _testClient;

        public static int MyMain ( string[] args )
        {
            int result = 0;

            Logger.Log( "MAIN: Creating test client..." );
            _testClient = new TestIpcClient();

            Logger.Log( "MAIN: Test client created. Waiting for remote command execution..." );

            try
            {
                var remoteCommand = args.Length == 0 ? "MyCoolCommand\0Arg1\0Arg2" : string.Join("\0", args);
                
                using( var task = _testClient.ExecuteRemoteCommand(
                    remoteCommand, _cancellationTokenSource.Token ) )
                {
                    task.Wait( _cancellationTokenSource.Token );
                    Logger.Log( "MAIN: Remote command finished." );

                    ProcessResponse( task.Result );
                }
            }
            catch( OperationCanceledException )
            {
                Logger.Log( "MAIN: Operation canceled." );
            }
            catch( Exception ex )
            {
                Logger.Log( "MAIN: Connection error: " + ex );
                result = 1;
            }

            Logger.Log( "MAIN: Exiting program..." );

            return result;
        }

        private static int ProcessResponse ( string response )
        {
            if( string.IsNullOrEmpty( response ) )
            {
                Logger.Log("NOTHING");
                return 1;
            }
            
            var tokens = response.Split('\0');
            var status = tokens[0];

            if( status == "OK" )
            {
                if (tokens.Length > 1)
                    Logger.Log("OK: " + tokens[1]);
                else
                    Logger.Log( "OK" );
                return 0;
            }

            if (status == "FAIL")
            {
                if (tokens.Length > 1)
                    Logger.Log("FAIL: {0}", tokens[1]);
                else
                    Logger.Log("FAIL");
                return 1;
            }

            Logger.Log("UNKNOWN");

            return 1;
        }

    }
}