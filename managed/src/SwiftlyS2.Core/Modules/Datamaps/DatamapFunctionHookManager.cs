namespace SwiftlyS2.Core.Datamaps;

public static class DatamapFunctionHookManager
{
    private static Dictionary<nint, nint> _hookMap = [];

    public static void AddHook( nint address, nint hook )
    {
        _hookMap[address] = hook;
    }

    public static void RemoveHook( nint address )
    {
        _hookMap.Remove(address);
    }

    public static bool TryGetHook( nint address, out nint hook )
    {
        return _hookMap.TryGetValue(address, out hook);
    }
}