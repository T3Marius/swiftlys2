using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Shared.Datamaps;

public interface IDatamapFunctionHookContext
{

}

public interface IDatamapFunctionHookContext<T> : IDatamapFunctionHookContext
    where T : ISchemaClass<T>
{

    public T SchemaObject { get; internal set; }

    public HookResult HookResult { get; set;  }

}