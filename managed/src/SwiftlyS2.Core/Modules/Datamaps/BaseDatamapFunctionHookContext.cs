using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Core.Datamaps;

internal class BaseDatamapFunctionHookContext<T> : IDatamapFunctionHookContext<T> where T : ISchemaClass<T>
{
    public T SchemaObject { get; set; }
    public HookResult HookResult { get; set; }
}