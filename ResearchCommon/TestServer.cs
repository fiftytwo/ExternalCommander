using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public abstract class TestServer : ITestServer
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;

        public void OnEnable ()
        {
            Logger.Log( "OnEnable" );
            if( _cancellationTokenSource != null )
                return;

            Logger.Log( "Create _cancellationTokenSource" );
            _cancellationTokenSource = new CancellationTokenSource();

            Logger.Log( "Executing commands processor" );
            _processingTask = ProcessRemoteCommands( _cancellationTokenSource.Token );
            Logger.Log( "Commands processor started" );
        }
        
        public void OnDisable ()
        {
            Logger.Log( "OnDisable" );
            if( _cancellationTokenSource == null )
                return;
            Logger.Log( "Canceling..." );
            _cancellationTokenSource.Cancel();
            _processingTask.Wait();
            Logger.Log( "Cancelled. Disposing processing task and cancellation source..." );
            _processingTask.Dispose();
            _processingTask = null;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            Logger.Log( "Disposed." );
        }

        protected abstract Task ProcessRemoteCommands ( CancellationToken token );

        protected string ExecuteCommand ( string commandWithArgs )
        {
            try
            {
                var tokens = commandWithArgs.Split( '\0' );

                var command = tokens[0];

                var sb = new StringBuilder( "EXECUTE " );
                sb.Append( command );
                sb.Append( "(" );
                if( tokens.Length > 0 )
                {
                    sb.Append( tokens[1] );
                    for( int i = 2; i < tokens.Length; ++i )
                    {
                        sb.Append( ", " );
                        sb.Append( tokens[i] );
                    }
                }

                sb.Append( ")" );

                Logger.Log( sb.ToString() );

                return "OK";
            }
            catch( Exception ex )
            {
                Logger.Log( "EXECUTE FAIL: " + ex );
                return "FAIL\0" + ex;
            }
        }
    }
}