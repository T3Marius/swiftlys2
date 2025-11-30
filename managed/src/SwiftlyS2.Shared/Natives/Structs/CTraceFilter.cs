using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Schemas;
using System.Runtime.InteropServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 64)]
public struct CTraceFilter
{
    [FieldOffset(0x0)] private nint _pVTable;
    [FieldOffset(0x8)] public RnQueryShapeAttr_t QueryShapeAttributes;
    [FieldOffset(0x38)] public bool IterateEntities;

    public CTraceFilter( bool checkIgnoredEntities = true )
    {
        _pVTable = checkIgnoredEntities ? CTraceFilterVTable.pCTraceFilterShouldHitFunctionCall : CTraceFilterVTable.pCTraceFilterVTable;
    }
}

internal static class CTraceFilterVTable
{
    public static nint pCTraceFilterVTable;
    public static nint pCTraceFilterShouldHitFunctionCall;

    [UnmanagedCallersOnly]
    public unsafe static void Destructor( CTraceFilter* filter, byte unk01 )
    {
        // do nothing
    }

    [UnmanagedCallersOnly]
    public unsafe static byte ShouldHitEntity()
    {
        return 1;
    }

    [UnmanagedCallersOnly]
    public unsafe static byte ShouldHitEntity( CTraceFilter* filter, nint entity )
    {
        var ent = Helper.AsSchema<CBaseEntity>(entity);
        if (ent == null || !ent.IsValid) return 0;

        return filter->QueryShapeAttributes.EntityIdsToIgnore[0] != ent.Index && filter->QueryShapeAttributes.EntityIdsToIgnore[1] != ent.Index ? (byte)1 : (byte)0;
    }

    static unsafe CTraceFilterVTable()
    {
        pCTraceFilterVTable = Marshal.AllocHGlobal(sizeof(nint) * 2);
        Span<nint> vtable = new((void*)pCTraceFilterVTable, 2);
        vtable[0] = (nint)(delegate* unmanaged< CTraceFilter*, byte, void >)(&Destructor);
        vtable[1] = (nint)(delegate* unmanaged< byte >)(&ShouldHitEntity);

        pCTraceFilterShouldHitFunctionCall = Marshal.AllocHGlobal(sizeof(nint) * 2);
        Span<nint> funcTable = new((void*)pCTraceFilterShouldHitFunctionCall, 2);
        funcTable[0] = (nint)(delegate* unmanaged< CTraceFilter*, byte, void >)(&Destructor);
        funcTable[1] = (nint)(delegate* unmanaged< CTraceFilter*, nint, byte >)(&ShouldHitEntity);
    }
}