using System;

namespace Fiftytwo
{
    static class ExternalCommanderProgram
    {
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine( "You must pass at least 3 arguments.\n" +
                    "The first argument is named pipe channel name.\n" +
                    "The second argument is a string representation of a type that is used with Type.GetType().\n" +
                    "The third argument is a method name to call.\n" +
                    "The rest arguments if specified will be passed inside the method as a string[]" );
                
                return 1;
            }

            try
            {
                var ipcChannelName = args[0];
                
                var packedArgs = string.Join( "\0", args, 1, args.Length - 1 );

                var ipcClient = IpcClient.Create( ipcChannelName );
                using( var stream = ipcClient.Connect() )
                {
                    stream.SendString(packedArgs);

                    var response = stream.ReceiveString();
                    var tokens = response.Split('\0');
                    var status = tokens[0];

                    if (status == "FAIL")
                    {
                        if (tokens.Length > 1)
                            Console.Error.WriteLine("FAIL: {0}", tokens[1]);
                        else
                            Console.Error.WriteLine("FAIL");
                        return 1;
                    }

                    if( status != "OK" )
                    {
                        Console.Error.WriteLine("NOTHING");
                        return 1;
                    }

                    if (tokens.Length > 1)
                        Console.WriteLine(tokens[1]);
                }
            }
            catch( Exception ex )
            {
                Console.Error.WriteLine("ERROR: {0}", ex);
                return 1;
            }

            return 0;
        }
    }
}
