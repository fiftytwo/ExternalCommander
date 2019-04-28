using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Fiftytwo
{
    public static class Logger
    {
        [ThreadStatic]
        private static StringBuilder _sb;
        
        public static void Log ( string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0 )
        {
            if( _sb == null )
                _sb = new StringBuilder();

            _sb.Length = 0;
            _sb.Append( Path.GetFileNameWithoutExtension( sourceFilePath ) );
            _sb.Append( '.' );
            _sb.Append( memberName );
            _sb.Append( ':' );
            _sb.Append( sourceLineNumber );
            _sb.Append( " [" );
            _sb.Append( Thread.CurrentThread.ManagedThreadId );
            _sb.Append( "]: " );
            _sb.AppendLine( message );
            
            Console.Write( _sb.ToString() );
        }
    }
}
