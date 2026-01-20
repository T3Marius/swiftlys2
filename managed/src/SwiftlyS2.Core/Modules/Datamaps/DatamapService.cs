using SwiftlyS2.Shared.Datamaps;

namespace SwiftlyS2.Core.Datamaps;

internal partial class DatamapService( DatamapFunctionService functions ) : IDatamapService
{
    public IDatamapFunctionService Functions { get; } = functions;
}