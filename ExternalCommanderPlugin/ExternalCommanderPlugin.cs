﻿#define VERBOSE_LOG

using System;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace Fiftytwo
{
    [InitializeOnLoad]
    internal class ExternalCommanderPlugin : ScriptableObject
    {
        private const string UnixDomainSocketPath = "Temp/com.fiftytwo.ExternalCommander.socket";
        private const string MemoryMappedFileNamePrefix = "com.fiftytwo.ExternalCommander.";

        private static ExternalCommanderPlugin _instance;

        private CancellationTokenSource _cts;

        static ExternalCommanderPlugin ()
        {
            SynchronizationContext.Current.Post( s =>
            {
                Log( $"Creating an instance of {typeof(ExternalCommanderPlugin)}..." );
                ScriptableObject.CreateInstance<ExternalCommanderPlugin>();
                Log( $"Instance of {typeof(ExternalCommanderPlugin)} has been created." );
            }, null );
        }

        private async void OnEnable ()
        {
            if( _instance != null )
            {
                Log( GetType() + " instance is already running, skip commands accepting" );
                return;
            }

            EditorApplication.quitting += OnQuitting;

            _instance = this;
            _cts = new CancellationTokenSource();

            using( var ipcServer = CreateIpcServer() )
            {
                while( !_cts.IsCancellationRequested )
                {
                    try
                    {
                        Log( $"Waiting for connection accept..." );
                        using( var stream = await ipcServer.AcceptAsync( _cts.Token ).ConfigureAwait( true ) )
                        {
                            Log( $"Connection accepted and Stream received." );

                            Log( $"Receiving request from client..." );
                            var request = stream.ReceiveString();
                            Log( $"Request received. Processing..." );
                            var response = ProcessRequest( request );
                            Log( $"Request processed. Responding..." );
                            stream.SendString( response );
                            Log( $"Response sent. Closing stream..." );
                        }

                        Log( $"Stream closed." );
                    }
                    catch( OperationCanceledException )
                    {
                        Log( $"Accept canceled, exiting accept loop..." );
                        break;
                    }
                    catch( Exception ex )
                    {
                        LogError( $"Break accept loop because of uexpected error: {ex}" );
                        break;
                    }
                }
            }
        }

        private IpcServer CreateIpcServer ()
        {
            var platformId = Environment.OSVersion.Platform;
            if( platformId == PlatformID.Unix || platformId == PlatformID.MacOSX )
            {
                Log( $"Create IpcServerUnixDomainSocket '{UnixDomainSocketPath}'" );
                return new IpcServerUnixDomainSocket( UnixDomainSocketPath );
            }

            var ipcChannelName = MemoryMappedFileNamePrefix + PlayerSettings.productGUID.ToString( "N" );
            Log( $"Create IpcServerMemoryMappedFile '{ipcChannelName}'" );
            return new IpcServerMemoryMappedFile( ipcChannelName );
        }

        private string ProcessRequest ( string packedArgs )
        {
            string response;

            try
            {
                Log( $"Execute: {packedArgs}" );
                var result = Execute( packedArgs );
                if( result == null )
                    response = "OK";
                else
                    response = "OK\0" + result;
            }
            catch( Exception ex )
            {
                response = "FAIL\0" + ex;
            }

            return response;
        }

        private static string Execute ( string packedArgs )
        {
            var tokens = packedArgs.Split('\0');
            var type = Type.GetType( tokens[0] );
            var method = type.GetMethod( tokens[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );

            object result;
            if( tokens.Length > 2 )
            {
                var args = new string[tokens.Length - 2];
                for( int i = args.Length; --i >= 0; )
                    args[i] = tokens[i + 2];
                result = method.Invoke( null, new object[] { args } );
            }
            else
            {
                result = method.Invoke( null, null );
            }

            return result?.ToString();
        }

        private void OnDisable ()
        {
            Log( $"OnDisable()" );
            EditorApplication.quitting -= OnQuitting;
            DisconnectAndCleanup();
        }

        private void OnQuitting ()
        {
            Log( $"OnQuitting()" );
            DisconnectAndCleanup();
        }

        private void DisconnectAndCleanup ()
        {
            if( _cts != null )
            {
                Log( $"Canceling server..." );
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
                _instance = null;
                Log( $"Server has been canceled." );
            }
        }

        private static int TestMethod ( params string[] args )
        {
            Debug.Log( $"Test Method!!! {args[0]}, {args[1]}" );

            return 1100;
        }

        [System.Diagnostics.Conditional( "VERBOSE_LOG" )]
        private static void Log ( string message )
        {
            Debug.Log( "[" +
                System.Threading.Thread.CurrentThread.ManagedThreadId + "] " + message );
        }

        private static void LogError ( string message )
        {
            Debug.LogError( "[" +
                System.Threading.Thread.CurrentThread.ManagedThreadId + "] " + message );
        }
    }
}
