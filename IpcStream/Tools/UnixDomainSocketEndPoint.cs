// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Fiftytwo
{
    /// <summary>Represents a Unix Domain Socket endpoint as a path.</summary>
    public sealed class UnixDomainSocketEndPoint : EndPoint
    {
        private const AddressFamily EndPointAddressFamily = AddressFamily.Unix;

        private static readonly bool s_isUnixPlatform;
        private static readonly int s_nativePathOffset;
        private static readonly int s_nativePathLength;
        private static readonly int s_nativeAddressSize;

        private static readonly Encoding s_pathEncoding = Encoding.UTF8;
        private static readonly Lazy<bool> s_udsSupported = new Lazy<bool>(() =>
        {
            try
            {
                new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified).Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        });

        private readonly Func<SocketAddress> _createSocketAddressForSerialize;

        private readonly string _path;
        private readonly byte[] _encodedPath;

        static UnixDomainSocketEndPoint()
        {
            var platformId = Environment.OSVersion.Platform;
            s_isUnixPlatform = platformId == PlatformID.Unix || platformId == PlatformID.MacOSX;

            if (s_isUnixPlatform)
            {
                s_nativePathOffset = 2; // offsetof(sun_path)
                s_nativePathLength = 104; // sizeof(sun_path)
                s_nativeAddressSize = s_nativePathOffset + s_nativePathLength; // sizeof(sockaddr_un)

                Debug.Assert(s_nativePathOffset >= 0, "Expected path offset to be positive");
                Debug.Assert(s_nativePathOffset + s_nativePathLength <= s_nativeAddressSize, "Expected address size to include all of the path length");
                Debug.Assert(s_nativePathLength >= 92, "Expected max path length to be at least 92"); // per http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/sys_un.h.html

                //Interop.Sys.GetDomainSocketSizes(out s_nativePathOffset, out s_nativePathLength, out s_nativeAddressSize);
                // from sys/un.h:
                // [XSI] Definitions for UNIX IPC domain.
                //struct  sockaddr_un {
                //    unsigned char   sun_len;        // sockaddr len including null
                //    sa_family_t     sun_family;     // [XSI] AF_UNIX
                //    char            sun_path[104];  // [XSI] path name (gag)
                //};
            }
            else
            {
                s_nativePathOffset = 2; // sizeof(sun_family)
                s_nativePathLength = 108; // sizeof(sun_path)
                s_nativeAddressSize = s_nativePathOffset + s_nativePathLength; // sizeof(sockaddr_un)

                // from afunix.h:
                //#define UNIX_PATH_MAX 108
                //typedef struct sockaddr_un
                //{
                //    ADDRESS_FAMILY sun_family;     // AF_UNIX
                //    char sun_path[UNIX_PATH_MAX];  // pathname
                //}
                //SOCKADDR_UN, *PSOCKADDR_UN;
            }
        }

        public UnixDomainSocketEndPoint(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (s_isUnixPlatform)
            {
                _createSocketAddressForSerialize = () =>
                    new SocketAddress(AddressFamily.Unix, s_nativePathOffset + _encodedPath.Length);
            }
            else
            {
                _createSocketAddressForSerialize = () =>
                    new SocketAddress(AddressFamily.Unix, s_nativeAddressSize);
            }

            // Pathname socket addresses should be null-terminated.
            // Linux abstract socket addresses start with a zero byte, they must not be null-terminated.
            bool isAbstract = IsAbstract(path);
            int bufferLength = s_pathEncoding.GetByteCount(path);
            if (!isAbstract)
            {
                // for null terminator
                bufferLength++;
            }

            if (path.Length == 0 || bufferLength > s_nativePathLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(path), path);
            }

            _path = path;
            _encodedPath = new byte[bufferLength];
            int bytesEncoded = s_pathEncoding.GetBytes(path, 0, path.Length, _encodedPath, 0);
            Debug.Assert(bufferLength - (isAbstract ? 0 : 1) == bytesEncoded);

            if (!s_udsSupported.Value)
            {
                throw new PlatformNotSupportedException();
            }
        }

        internal UnixDomainSocketEndPoint(SocketAddress socketAddress)
        {
            if (socketAddress == null)
            {
                throw new ArgumentNullException(nameof(socketAddress));
            }

            if (socketAddress.Family != EndPointAddressFamily ||
                socketAddress.Size > s_nativeAddressSize)
            {
                throw new ArgumentOutOfRangeException(nameof(socketAddress));
            }

            if (socketAddress.Size > s_nativePathOffset)
            {
                _encodedPath = new byte[socketAddress.Size - s_nativePathOffset];
                for (int i = 0; i < _encodedPath.Length; i++)
                {
                    _encodedPath[i] = socketAddress[s_nativePathOffset + i];
                }

                // Strip trailing null of pathname socket addresses.
                int length = _encodedPath.Length;
                if (!IsAbstract(_encodedPath))
                {
                    // Since this isn't an abstract path, we're sure our first byte isn't 0.
                    while (_encodedPath[length - 1] == 0)
                    {
                        length--;
                    }
                }
                _path = s_pathEncoding.GetString(_encodedPath, 0, length);
            }
            else
            {
                _encodedPath = Array.Empty<byte>();
                _path = string.Empty;
            }
        }

        public override SocketAddress Serialize()
        {
            SocketAddress result = _createSocketAddressForSerialize();

            for (int index = 0; index < _encodedPath.Length; index++)
            {
                result[s_nativePathOffset + index] = _encodedPath[index];
            }

            return result;
        }

        public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

        public override AddressFamily AddressFamily => EndPointAddressFamily;

        public override string ToString()
        {
            bool isAbstract = IsAbstract(_path);
            if (isAbstract)
            {
                return string.Concat("@", _path.Substring(1));
            }
            else
            {
                return _path;
            }
        }

        private static bool IsAbstract(string path) => path.Length > 0 && path[0] == '\0';

        private static bool IsAbstract(byte[] encodedPath) => encodedPath.Length > 0 && encodedPath[0] == 0;
    }
}
