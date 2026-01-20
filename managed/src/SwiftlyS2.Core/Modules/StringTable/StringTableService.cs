using SwiftlyS2.Core.Extensions;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.StringTable;

namespace SwiftlyS2.Core.StringTable;

internal class StringTableService : IStringTableService
{
    private readonly INetMessageService _netMessageService;
    public StringTableService( INetMessageService netMessageService )
    {
        _netMessageService = netMessageService;
    }

    public IStringTable? FindTable( string tableName )
    {
        var ptr = NativeStringTable.ContainerFindTable(tableName);
        return !ptr.IsValidPtr() ? null : (IStringTable)new StringTable(ptr, _netMessageService);
    }

    public IStringTable? FindTableById( int tableId )
    {
        var ptr = NativeStringTable.ContainerGetTableById(tableId);
        return !ptr.IsValidPtr() ? null : (IStringTable)new StringTable(ptr, _netMessageService);
    }
}