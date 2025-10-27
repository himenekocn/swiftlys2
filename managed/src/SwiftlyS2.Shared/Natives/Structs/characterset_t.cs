using System.Runtime.InteropServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential, Size = 256)]
public unsafe struct characterset_t
{
    private fixed byte set[256];
}