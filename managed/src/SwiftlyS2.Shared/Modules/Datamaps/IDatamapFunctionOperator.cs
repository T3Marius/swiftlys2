using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Shared.Datamaps;

public interface IDatamapFunctionOperator<T, out K>
    where T : ISchemaClass<T>
    where K : IDatamapFunctionHookContext<T>

{

    public void HookPre( Action<K> callback );

    public void HookPost( Action<K> callback );

    public void Invoke( T schemaObject );

    public void InvokeOriginal( T schemaObject );

}