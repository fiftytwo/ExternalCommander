using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Fiftytwo
{
    public class MemoryMappedFileStream : Stream
    {
        public const long ReservedHeaderSize = 2 * sizeof( long );
        public const long DefaultCapacity = 0x10000 - ReservedHeaderSize;
        
        private const string AccessLockSuffix = "L";
        private const string EmptyEventSuffix = "E";
        private const string FullEventSuffix = "F";

        private const string Suffix0 = "0";
        private const string Suffix1 = "1";

        private readonly long _dataCapacityR;
        private readonly long _startOffsetR;
        private readonly long _countOffsetR;
        private readonly long _dataCapacityW;
        private readonly long _startOffsetW;
        private readonly long _countOffsetW;
        
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessorR;
        private MemoryMappedViewAccessor _accessorW;
        private Semaphore _semaphoreR;
        private Semaphore _semaphoreW;
        private EventWaitHandle _nonEmptyEventR;
        private EventWaitHandle _nonEmptyEventW;
        private EventWaitHandle _nonFullEventR;
        private EventWaitHandle _nonFullEventW;
        
        private bool _isDisposed;

        public MemoryMappedFileStream ( string mapName, bool isServer,
            long readCapacity = DefaultCapacity, long writeCapacity = DefaultCapacity )
        {
            if( string.IsNullOrEmpty( mapName ) )
                throw new ArgumentNullException( nameof( mapName ) );
            if( readCapacity <= 0 )
                throw new ArgumentOutOfRangeException( nameof( readCapacity ), "<= 0" );
            if( writeCapacity <= 0 )
                throw new ArgumentOutOfRangeException( nameof( writeCapacity ), "<= 0" );
            
            _dataCapacityR = readCapacity;
            _startOffsetR = _dataCapacityR;
            _countOffsetR = _startOffsetR + sizeof( long );

            _dataCapacityW = writeCapacity;
            _startOffsetW = _dataCapacityW;
            _countOffsetW = _startOffsetW + sizeof( long );

            try
            {
                string readSuffix;
                string writeSuffix;
                _mmf = MemoryMappedFile.CreateOrOpen( mapName, readCapacity + writeCapacity + 2 * ReservedHeaderSize );
                if( isServer )
                {
                    _accessorR = _mmf.CreateViewAccessor( 0, readCapacity + ReservedHeaderSize );
                    _accessorW = _mmf.CreateViewAccessor( _accessorR.Capacity, writeCapacity + ReservedHeaderSize );
                    readSuffix = Suffix0;
                    writeSuffix = Suffix1;
                }
                else
                {
                    _accessorW = _mmf.CreateViewAccessor( 0, writeCapacity + ReservedHeaderSize );
                    _accessorR = _mmf.CreateViewAccessor( _accessorW.Capacity, readCapacity + ReservedHeaderSize );
                    writeSuffix = Suffix0;
                    readSuffix = Suffix1;
                }
                _semaphoreR = new Semaphore( 1, 1, mapName + readSuffix + AccessLockSuffix );
                _semaphoreW = new Semaphore( 1, 1, mapName + writeSuffix + AccessLockSuffix );
                _nonEmptyEventR = new EventWaitHandle( false,
                    EventResetMode.AutoReset, mapName + readSuffix + EmptyEventSuffix );
                _nonEmptyEventW = new EventWaitHandle( false,
                    EventResetMode.AutoReset, mapName + writeSuffix + EmptyEventSuffix );
                _nonFullEventR = new EventWaitHandle( true, EventResetMode.AutoReset, mapName + readSuffix + FullEventSuffix );
                _nonFullEventW = new EventWaitHandle( true, EventResetMode.AutoReset, mapName + writeSuffix + FullEventSuffix );
            }
            catch
            {
                _nonFullEventW?.Dispose();
                _nonFullEventR?.Dispose();
                _nonEmptyEventW?.Dispose();
                _nonEmptyEventR?.Dispose();
                _semaphoreW?.Dispose();
                _semaphoreR?.Dispose();
                _accessorW?.Dispose();
                _accessorR?.Dispose();
                _mmf?.Dispose();
                throw;
            }
        }

        public override void Flush ()
        {
            if( _isDisposed )
                throw new ObjectDisposedException( "Stream has been closed" );
        }

        public override int Read ( byte[] buffer, int offset, int count )
        {
            if( _isDisposed )
                throw new ObjectDisposedException( "Stream has been closed" );
            if( !CanRead )
                throw new NotSupportedException( "Stream does not support reading" );
            if( buffer == null )
                throw new ArgumentNullException( nameof( buffer ) );
            if( offset < 0 )
                throw new ArgumentOutOfRangeException( nameof( offset ), "< 0" );
            if( count < 0 )
                throw new ArgumentOutOfRangeException( nameof( count ), "< 0" );
            var length = buffer.Length;
            if( offset > length )
                throw new ArgumentException( "destination offset is beyond array size" );
            if( offset > length - count )
                throw new ArgumentException( "Reading would overrun buffer" );

            if( count == 0 )
                return 0;

            WaitHandle.WaitAll( new WaitHandle[] { _semaphoreR, _nonEmptyEventR } );
            
            long start = StartR;
            long countAvailable = CountR;
            long countToRead = count < countAvailable ? count : countAvailable;
            long countToRead2 = 0;
            if( start + countToRead > _dataCapacityR )
            {
                countToRead2 = start + countToRead - _dataCapacityR;
                countToRead -= countToRead2;
            }

            _accessorR.ReadArray( start, buffer, offset, ( int )countToRead );
            start += countToRead;
            if( start == _dataCapacityR )
                start = 0;

            if( countToRead2 > 0 )
            {
                offset += ( int )countToRead;
                _accessorR.ReadArray( start, buffer, offset, ( int )countToRead2 );
                start += countToRead2;
            }

            countToRead += countToRead2;
            countAvailable -= countToRead;

            StartR = start;
            CountR = countAvailable;

            if( countAvailable > 0 )
                _nonEmptyEventR.Set();
            if( countAvailable < _dataCapacityR )
                _nonFullEventR.Set();

            _semaphoreR.Release();

            return ( int )countToRead;
        }

        public override void Write ( byte[] buffer, int offset, int count )
        {
            if( _isDisposed )
                throw new ObjectDisposedException( "Stream has been closed" );
            if( !CanWrite )
                throw new NotSupportedException( "Stream does not support writing" );
            if( buffer == null )
                throw new ArgumentNullException( nameof ( buffer ) );
            if( offset < 0 )
                throw new ArgumentOutOfRangeException( nameof ( offset ), "< 0" );
            if( count < 0 )
                throw new ArgumentOutOfRangeException( nameof ( count ), "< 0" );
            if( offset > buffer.Length - count )
                throw new ArgumentException( "Reading would overrun buffer" );

            while( count > 0 )
            {
                WaitHandle.WaitAll( new WaitHandle[] { _semaphoreW, _nonFullEventW } );

                long start = StartW;
                long countAvailable = CountW;
                long startWrite = start + countAvailable;
                if( startWrite >= _dataCapacityW )
                    startWrite -= _dataCapacityW;
                long willWrite = _dataCapacityW - countAvailable;
                if( willWrite > count )
                    willWrite = count;
                long willWrite2 = 0;
                if( startWrite + willWrite > _dataCapacityW )
                {
                    willWrite2 = startWrite + willWrite - _dataCapacityW;
                    willWrite -= willWrite2;
                }
                
                _accessorW.WriteArray( startWrite, buffer, offset, ( int )willWrite );
                offset += ( int )willWrite;

                if( willWrite2 > 0 )
                {
                    _accessorW.WriteArray( 0, buffer, offset, ( int )willWrite2 );
                    offset += ( int )willWrite2;
                }

                willWrite += willWrite2;
                countAvailable += willWrite;
                count -= ( int )willWrite;
                CountW = countAvailable;
                
                if( countAvailable > 0 )
                    _nonEmptyEventW.Set();
                if( countAvailable < _dataCapacityW )
                    _nonFullEventW.Set();

                _semaphoreW.Release();
            }
        }
        
        protected override void Dispose ( bool disposing )
        {
            if( _isDisposed )
                return;

            if( disposing )
            {
                _nonFullEventW?.Dispose();
                _nonFullEventR?.Dispose();
                _nonEmptyEventW?.Dispose();
                _nonEmptyEventR?.Dispose();
                _semaphoreW?.Dispose();
                _semaphoreR?.Dispose();
                _accessorW?.Dispose();
                _accessorR?.Dispose();
                _mmf?.Dispose();
            }

            _isDisposed = true;

            base.Dispose( disposing );
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;


        private long StartR
        {
            get
            {
                return _accessorR.ReadInt64( _startOffsetR );
            }
            set
            {
                _accessorR.Write( _startOffsetR, value );
            }
        }
        
        private long CountR
        {
            get
            {
                return _accessorR.ReadInt64( _countOffsetR );
            }
            set
            {
                _accessorR.Write( _countOffsetR, value );
            }
        }
        
        private long StartW
        {
            get
            {
                return _accessorW.ReadInt64( _startOffsetW );
            }
            set
            {
                _accessorW.Write( _startOffsetW, value );
            }
        }
        
        private long CountW
        {
            get
            {
                return _accessorW.ReadInt64( _countOffsetW );
            }
            set
            {
                _accessorW.Write( _countOffsetW, value );
            }
        }

#region Not Supported

        public override long Seek ( long offset, SeekOrigin origin )
        {
            throw new NotSupportedException( nameof( Seek ) + " is not supported in " + GetType() );
        }

        public override void SetLength ( long value )
        {
            throw new NotSupportedException( nameof( SetLength ) + " is not supported in " + GetType() );
        }

        public override long Length
        {
            get { throw new NotSupportedException( nameof( Length ) + " is not supported in " + GetType() ); }
        }

        public override long Position
        {
            get { throw new NotSupportedException( nameof( Position ) + " is not supported in " + GetType() ); }
            set { throw new NotSupportedException( nameof( Position ) + " is not supported in " + GetType() ); }
        }

#endregion
    }
}