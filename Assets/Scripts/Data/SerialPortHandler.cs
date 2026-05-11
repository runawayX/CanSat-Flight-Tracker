using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class SerialPortHandler : IDisposable
{
    #region DLL
    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateListener(string portName, int baudRate);
    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DestroyListener(IntPtr listener);

    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool IsEnabled(IntPtr listener);
    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Status(IntPtr listener);

    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool TryOutputLatestPacket(IntPtr listener, byte[] output, int outputLimit);

    [DllImport("UnitySerialReader.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr Handshake(IntPtr listener);
    #endregion

    public enum StatusCode
    {
        NULL = -1,

        DISABLED = 0,
        RUNNING = 1,
        FAIL_READ = 2,
        FAIL_INVALID_HANDLE = 3,
        FAIL_GET_SERIALPARAMS = 4,
        FAIL_SET_SERIALPARAMS = 5
    }

    private IntPtr _listener = IntPtr.Zero;
    private object _listenerLock = new object();

    private readonly byte[] _packetBuffer;

    private bool _disposed;

    public SerialPortHandler(string portName, int baudRate, int bufferSize)
    {
        if (_listener == IntPtr.Zero) _listener = CreateListener(portName, baudRate);
        else Debug.LogError("Duplicate initialization on serial port handler!");

        _packetBuffer = new byte[bufferSize];

        _disposed = false;
    }

    public bool TryPacket_P(out string textPacket)
    {
        lock (_listenerLock)
        {
            _packetBuffer[0] = 0;
            if (_listener != IntPtr.Zero && TryOutputLatestPacket(_listener, _packetBuffer, _packetBuffer.Length))
            {
                textPacket = System.Text.Encoding.ASCII.GetString(_packetBuffer, 0, Array.IndexOf(_packetBuffer, (byte) 0));
                return true;
            }

            textPacket = null;
            return false;
        }
    }

    public string GetHandshake_P()
    {
        if (_listener != IntPtr.Zero) return Marshal.PtrToStringAnsi(Handshake(_listener));
        return "NULL";
    }

    public bool IsRunning_P()
    {
        if (_listener != IntPtr.Zero) return IsEnabled(_listener);
        return false;
    }

    public int StatusID_P()
    {
        if (_listener != IntPtr.Zero) return Status(_listener);
        return -1;
    }

    public StatusCode StatusCode_P()
    {
        if (_listener != IntPtr.Zero) return (StatusCode) Status(_listener);
        return StatusCode.NULL;
    }

    private void Disable_I()
    {
        lock (_listenerLock)
        {
            if (_listener == IntPtr.Zero) return;

            DestroyListener(_listener);
            _listener = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Disable_I();
        GC.SuppressFinalize(this);

        _disposed = true;
    }

    ~SerialPortHandler()
    {
        Dispose();
    }
}
