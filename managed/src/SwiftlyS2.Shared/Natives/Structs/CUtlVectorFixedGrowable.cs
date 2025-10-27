using System.Runtime.InteropServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public struct CUtlVectorFixedGrowable<T>
{
    private int _size;
    private CUtlMemoryFixedGrowable<T> _memory;

    public CUtlVectorFixedGrowable(int maxSize, int growSize = 0)
    {
        _memory = new CUtlMemoryFixedGrowable<T>(maxSize, growSize, maxSize);
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
            unsafe
            {
                return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>((byte*)_memory.Base + index * Marshal.SizeOf<T>());
            }
        }
    }

    public readonly int Count => _size;
    public readonly nint Base => _memory.Base;
}