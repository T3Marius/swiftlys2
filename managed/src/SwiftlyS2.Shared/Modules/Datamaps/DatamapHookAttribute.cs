using SwiftlyS2.Shared.Misc;

namespace SwiftlyS2.Shared.Datamaps;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class DatamapHookAttribute : Attribute
{
    public HookMode HookMode { get; }

    public DatamapHookAttribute( HookMode hookMode )
    {
        HookMode = hookMode;
    }
}