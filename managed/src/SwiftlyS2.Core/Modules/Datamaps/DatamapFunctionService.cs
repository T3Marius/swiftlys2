using SwiftlyS2.Core.Services;
using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Profiler;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Datamaps;

internal partial class DatamapFunctionService( DatamapFunctionManager manager, CoreContext ctx, IContextedProfilerService profiler ) : IDisposable, IDatamapFunctionService
{
    public void Dispose()
    {
        manager.Unload(ctx.Name);
    }
}