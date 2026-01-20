using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Core.Datamaps;

namespace SwiftlyS2.Core.Hosting;

internal static class DatamapFunctionManagerInjection
{
    public static IServiceCollection AddDatamapFunctionManager( this IServiceCollection self )
    {
        _ = self.AddSingleton<DatamapFunctionManager>();
        return self;
    }
}