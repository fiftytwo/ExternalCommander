using System;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Fiftytwo
{
    public static class TestEntryServer
    {
        private static ManualResetEvent _exitProgramEvent = new ManualResetEvent( false );
        private static ITestServer _testServer;

        public static int MyMain ( string[] args )
        {
            Task.Run( new Action( WaitForExitCommand ) );

            var clientTask = Task.Run( () =>
            {
                //return;
                Thread.Sleep( 3000 );

                TestEntryClient.MyMain( args );
            } );

            Logger.Log( "MAIN: Creating test server..." );
            _testServer = new TestIpcServer();
            _testServer.OnEnable();
            Logger.Log( "MAIN: Test server created and enabled. Processing commands..." );

            Logger.Log( "MAIN: Waiting for client task completion..." );
            clientTask.Wait();
            Logger.Log( "MAIN: Client task completed." );

            Logger.Log( "MAIN: Waiting for exit hot key (Ctrl+X)..." );
            _exitProgramEvent.WaitOne();

            Logger.Log( "MAIN: Exit command received. Disabling ResearchTestServer..." );
            _testServer.OnDisable();
            Logger.Log( "MAIN: ResearchTestServer disabled." );
            Logger.Log( "MAIN: Exiting program..." );

            return 0;
        }

        private static void WaitForExitCommand ()
        {
            for ( var keyInfo = Console.ReadKey( false );
                !( keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers == ConsoleModifiers.Control );
                keyInfo = Console.ReadKey( false ) )
            {
                Logger.Log( $"KEY: key={keyInfo.Key} mod={keyInfo.Modifiers} char={keyInfo.KeyChar}" );
                
                if( keyInfo.Key == ConsoleKey.Y && keyInfo.Modifiers == ConsoleModifiers.Control )
                    _testServer.OnDisable();
            }

            Logger.Log( "MAIN: Raise exit event." );

            _exitProgramEvent.Set();
        }
    }
}
