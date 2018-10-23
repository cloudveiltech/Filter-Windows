using System;
using System.Runtime.InteropServices;

namespace Filter.Platform.Mac
{
    public delegate void ConnectionCallbackDelegate();

    internal static class NativeIPCClientImpl
    {

        [DllImport(Platform.NativeLib)]
        public static extern IntPtr CreateIPCClient(MessageCallbackDelegate callbackDelegate, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_Connect")]
        public static extern void Connect(IntPtr handle, string serverName);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_Disconnect")]
        public static extern void Disconnect(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_Send")]
        public static extern void Send(IntPtr handle, byte[] data, int dataLength, bool isBroadcast);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_SetCallback")]
        public static extern void SetCallback(IntPtr handle, MessageCallbackDelegate callbackDelegate);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_Release")]
        public static extern void Release(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_IsConnected")]
        public static extern bool IsConnected(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPCClient_StartLoop")]
        public static extern IntPtr StartLoop(IntPtr handle);

        [DllImport(Platform.NativeLib, EntryPoint = "IPC_StopLoop")]
        public static extern void StopLoop(IntPtr thread);
    }
}
