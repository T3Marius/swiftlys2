using SwiftlyS2.Core.Hooks;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Datamaps;

internal partial class DatamapFunctionManager
{

    public event Action<string> OnPluginUnloaded;

    public void Unload( string pluginId )
    {
        OnPluginUnloaded?.Invoke(pluginId);
    }
}