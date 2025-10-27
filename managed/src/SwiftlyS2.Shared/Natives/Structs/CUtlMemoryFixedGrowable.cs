using System.Runtime.InteropServices;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public struct CUtlMemoryFixedGrowable<T>
{
    private CUtlMemory<T> _baseMemory;
    private nint _fixedMemory;

    public CUtlMemoryFixedGrowable(int size, int growSize = 0, int initSize = 0)
    {
        var elementSize = SchemaSize.Get<T>();
        _fixedMemory = NativeAllocator.Alloc((nuint)(size * elementSize));
        _baseMemory = new CUtlMemory<T>(_fixedMemory, size, false);
    }

    public nint Base => _baseMemory.Base;
    public int Count => _baseMemory.Count;
}