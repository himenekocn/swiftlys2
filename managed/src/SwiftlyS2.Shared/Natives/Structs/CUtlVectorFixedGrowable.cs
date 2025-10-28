using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct CUtlVectorFixedGrowable<T, TBuffer>
    where T : unmanaged
    where TBuffer : unmanaged
{
    private int _size;
    private CUtlMemoryFixedGrowable<T, TBuffer> _memory;

    public CUtlVectorFixedGrowable(int maxSize, int growSize = 0)
    {
        _memory = new CUtlMemoryFixedGrowable<T, TBuffer>(maxSize, growSize);
        _size = 0;
    }

    public void SetSize(int size)
    {
        _size = size;
    }

    public void RemoveAll()
    {
        _size = 0;
    }

    public int AddToTail(nint value)
    {
        int idx = _size;
        _size++;
        this[idx] = (T)(object)value;
        return idx;
    }

    public ref T this[int index]
    {
        get
        {
            return ref Unsafe.AsRef<T>((void*)(_memory.Base + index * sizeof(T)));
        }
    }

    public readonly int Count => _size;
    public readonly nint Base => _memory.Base;
}