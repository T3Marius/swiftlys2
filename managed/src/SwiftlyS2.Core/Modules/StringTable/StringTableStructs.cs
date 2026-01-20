using System.Runtime.InteropServices;

namespace SwiftlyS2.Core.StringTable;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct StringUserData_t
{
    public uint m_cbDataSize;
    public void* m_pRawData;
};
