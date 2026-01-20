using System.Runtime.InteropServices;
using Spectre.Console;
using SwiftlyS2.Core.Extensions;
using SwiftlyS2.Core.Hooks;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Profiler;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Core.Datamaps;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void StubDelegate( nint a1 );

internal class BaseDatamapFunction<T, K>
    where T : ISchemaClass<T>
    where K : IDatamapFunctionHookContext<T>, new()
{
    public uint Hash { get; }
    private bool _Initialized { get; set; }
    private bool _Hooked { get; set; }
    private nint _OriginalFunctionPtr { get; set; }
    private DatamapFunctionManager _Manager { get; set; }

    private StubDelegate? _keptAliveDelegate;

    private readonly Dictionary<string, BaseDatamapFunctionOperator<T, K>> _Operators = [];

    public BaseDatamapFunction( DatamapFunctionManager manager, uint hash )
    {
        _Manager = manager;
        Hash = hash;
        _Manager.OnPluginUnloaded += Unregister;
    }

    public IDatamapFunctionOperator<T, K> Get( string pluginId, IContextedProfilerService profiler )
    {
        if (!_Operators.TryGetValue(pluginId, out var op))
        {
            op = new BaseDatamapFunctionOperator<T, K>(this, profiler);
            _Operators.Add(pluginId, op);
        }
        return op;
    }

    public void Unregister( string pluginId )
    {
        if (!_Operators.TryGetValue(pluginId, out var op)) return;
        op.Dispose();
        var _ = _Operators.Remove(pluginId);
    }

    private nint GetDatamapFunctionAddressPtr()
    {
        return NativeSchema.GetDatamapFunction(Hash);
    }

    private void Stub( nint a1 )
    {
        try
        {
            foreach (var op in _Operators.Values)
            {
                if (!op.CallbackPre(a1)) return;
            }
            InvokeOriginal(a1);
            foreach (var op in _Operators.Values)
            {
                op.CallbackPost(a1);
            }
        }
        catch (Exception e)
        {
            if (!GlobalExceptionHandler.Handle(e)) return;
            AnsiConsole.WriteException(e);
        }
    }

    internal void Initialize()
    {
        if (_Initialized) return;
        _OriginalFunctionPtr = GetDatamapFunctionAddressPtr().Read<nint>();
        if (_OriginalFunctionPtr == nint.Zero)
        {
            throw new Exception($"Failed to get the address of the function {Hash}");
        }
        _Initialized = true;
    }

    internal void Invoke( nint a1 )
    {
        Initialize();
        Stub(a1);
    }

    internal void InvokeOriginal( nint a1 )
    {
        Initialize();
        unsafe
        {
            ((delegate* unmanaged< nint, void >)_OriginalFunctionPtr)(a1);
        }
    }

    internal void Hook()
    {
        if (_Hooked) return;
        Initialize();
        var func = new StubDelegate(Stub);
        _keptAliveDelegate = func;
        var ptr = Marshal.GetFunctionPointerForDelegate(func);
        DatamapFunctionHookManager.AddHook(_OriginalFunctionPtr, ptr);
        _Hooked = true;
    }


}