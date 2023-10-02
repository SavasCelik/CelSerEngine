﻿using CelSerEngine.Core.Native;
using System.Runtime.InteropServices;

namespace CelSerEngine.Core.Scripting;
public class MemoryManager
{
    private readonly IntPtr _processHandle;
    private readonly INativeApi _nativeApi;

    public MemoryManager(IntPtr processHandle, INativeApi nativeApi)
    {
        _processHandle = processHandle;
        _nativeApi = nativeApi;
    }

    public T ReadMemoryAt<T>(IntPtr memoryAddress)
        where T : struct
    {
        var typeSize = Marshal.SizeOf(typeof(T));
        var bytes = _nativeApi.ReadVirtualMemory(_processHandle, memoryAddress, (uint)typeSize);
        IntPtr ptr = Marshal.AllocHGlobal(typeSize);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        var result = Marshal.PtrToStructure<T>(ptr);
        Marshal.FreeHGlobal(ptr);

        return result;
    }

    public void WriteMemoryAt<T>(IntPtr memoryAddress, T newValue)
        where T : struct
    {
        _nativeApi.WriteMemory(_processHandle, memoryAddress, newValue);
    }
}
