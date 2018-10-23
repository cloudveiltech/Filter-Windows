using System;
using System.Runtime.InteropServices;

namespace Filter.Platform.Mac
{
    public delegate void MessageCallbackDelegate(IntPtr data, int dataLength);

    internal static class NativeIPCServerImpl
    {

        /// <summary>
        /// Creates an IPC server with the specified server name.
        /// </summary>
        /// <returns>The IPC Server</returns>
        /// <param name="serverName">The Mach port name to use.</param>
        /// <param name="callback">The callback to use when this IPC server receives a message.</param>
        [DllImport(Platform.NativeLib)]
        public static extern IntPtr CreateIPCServer(string serverName, MessageCallbackDelegate callback, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect);

        /// <summary>
        /// Sends the specified data to all registered IPC clients.
        /// </summary>
        /// <param name="handle">Handle.</param>
        /// <param name="data">Data.</param>
        /// <param name="dataLength">Data length.</param>
        [DllImport(Platform.NativeLib, EntryPoint = "IPCServer_SendToAll")]
        public static extern void SendToAll(IntPtr handle, byte[] data, int dataLength);

        /// <summary>
        /// Set the message callback if needs be.
        /// </summary>
        /// <param name="">.</param>
        [DllImport(Platform.NativeLib, EntryPoint = "IPCServer_SetCallback")]
        public static extern void SetCallback(IntPtr handle, MessageCallbackDelegate callback);

        /// <summary>
        /// Release the IPC server handle.
        /// </summary>
        /// <param name="handle">The IPC server handle</param>
        [DllImport(Platform.NativeLib, EntryPoint = "IPCServer_Release")]
        public static extern void Release(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_StartLoop")]
        public static extern IntPtr StartLoop(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPC_StopLoop")]
        public static extern void StopLoop(IntPtr thread);
    }
}
