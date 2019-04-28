using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public static class StreamExtension
    {
        public static void SendString ( this Stream stream, string str )
        {
            var bytes = ToBytesWithIntCountPrefix( str );
            stream.Write( bytes, 0, bytes.Length );
        }

        public static string ReceiveString ( this Stream stream )
        {
            var bytes = new byte[sizeof( int )];
            ReceiveBytes( stream, bytes, 0, sizeof( int ) );
            var count = BitConverter.ToInt32( bytes, 0 );

            if ( bytes.Length < count )
                bytes = new byte[count];
            ReceiveBytes( stream, bytes, 0, count );

            return Encoding.UTF8.GetString( bytes, 0, count );
        }

        public static void ReceiveBytes ( this Stream stream, byte[] bytes, int offset, int count )
        {
            for ( int totalReceived = 0; totalReceived < count; )
            {
                totalReceived += stream.Read( bytes, offset + totalReceived, count - totalReceived );
            }
        }

        public static async Task SendStringAsync ( this Stream stream, string str,
            CancellationToken token = default( CancellationToken ) )
        {
            var bytes = ToBytesWithIntCountPrefix( str );
            await stream.WriteAsync( bytes, 0, bytes.Length, token );
        }

        public static async Task<string> ReceiveStringAsync ( this Stream stream,
            CancellationToken token = default( CancellationToken ) )
        {
            var bytes = new byte[sizeof( int )];
            await ReceiveBytesAsync( stream, bytes, 0, sizeof( int ), token );
            var count = BitConverter.ToInt32( bytes, 0 );

            if ( bytes.Length < count )
                bytes = new byte[count];
            await ReceiveBytesAsync( stream, bytes, 0, count, token );

            return Encoding.UTF8.GetString( bytes, 0, count );
        }

        public static async Task ReceiveBytesAsync ( this Stream stream, byte[] bytes, int offset, int count,
            CancellationToken token = default( CancellationToken ) )
        {
            for ( int totalReceived = 0; totalReceived < count; )
            {
                totalReceived += await stream.ReadAsync( bytes, offset + totalReceived, count - totalReceived, token );
            }
        }

        private static byte[] ToBytesWithIntCountPrefix ( string str )
        {
            var count = Encoding.UTF8.GetByteCount( str );
            var countAsBytes = BitConverter.GetBytes( count );

            var bytes = new byte[sizeof(int) + count];
            for( int i = sizeof( int ); --i >= 0; )
                bytes[i] = countAsBytes[i];

            Encoding.UTF8.GetBytes( str, 0, str.Length, bytes, sizeof( int ) );

            return bytes;
        }
    }
}
