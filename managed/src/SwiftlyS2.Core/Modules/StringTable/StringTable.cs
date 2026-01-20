using SwiftlyS2.Core.Extensions;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Core.Natives.NativeObjects;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.StringTable;

namespace SwiftlyS2.Core.StringTable;

internal class StringTable( nint handle, INetMessageService netMessageService ) : NativeHandle(handle), IStringTable
{

    public int TableId => NativeStringTable.GetTableId(handle);

    public string TableName => NativeStringTable.GetTableName(handle);

    public int NumStrings => NativeStringTable.GetNumStrings(handle);

    public int? FindStringIndex( string str )
    {
        var index = NativeStringTable.FindStringIndex(handle, str);
        return index == -1 ? null : index;
    }

    public bool IsStringIndexValid( int index ) => NativeStringTable.IsStringIndexValid(handle, index);

    public string? GetString( int index )
    {
        if (!IsStringIndexValid(index))
        {
            return null;
        }
        return NativeStringTable.GetString(handle, index);
    }

    public bool TryGetStringUserData( int index, out StringTableOutUserData result )
    {
        if (!IsStringIndexValid(index))
        {
            result = default;
            return false;
        }
        var userData = NativeStringTable.GetStringUserData(handle, index);
        if (!userData.IsValidPtr())
        {
            result = default;
            return false;
        }
        unsafe
        {
            var ptr = (StringUserData_t*)userData;
            if (ptr->m_pRawData is null)
            {
                result = default;
                return false;
            }
            result = new(new(ptr->m_pRawData, (int)ptr->m_cbDataSize), true);
            return true;
        }
    }

    public StringTableOutUserData GetStringUserData( int index )
    {
        if (!TryGetStringUserData(index, out var result))
        {
            return default;
        }
        return result;
    }

    public bool TryGetStringUserData( string str, out StringTableOutUserData result )
    {
        var index = FindStringIndex(str);
        if (index is null)
        {
            result = default;
            return false;
        }
        return TryGetStringUserData(index!.Value, out result);
    }

    public StringTableOutUserData GetStringUserData( string str )
    {
        if (!TryGetStringUserData(str, out var result))
        {
            return default;
        }
        return result;
    }

    public bool SetStringUserData( int index, StringTableUserData userData, bool forceOverride = true )
    {
        if (!IsStringIndexValid(index))
        {
            throw new ArgumentException("Failed to set string user data. String index is invalid.");
        }
        unsafe
        {
            fixed (byte* userDataPtr = userData.Data)
            {
                return NativeStringTable.SetStringUserData(handle, index, (nint)userDataPtr, userData.Data.Length, forceOverride);
            }
        }
    }

    public bool SetStringUserData( string str, StringTableUserData userData, bool forceOverride = true )
    {
        var index = FindStringIndex(str);
        if (index is null)
        {
            throw new ArgumentException("Failed to set string user data. String is not found in string table.");
        }
        return SetStringUserData(index!.Value, userData, forceOverride);
    }

    public bool SetOrAddStringUserData( string str, StringTableUserData userData, bool forceOverride = true )
    {
        var index = GetOrAddString(str);
        return SetStringUserData(index, userData, forceOverride);
    }

    public int GetOrAddString( string str )
    {
        return NativeStringTable.AddString(handle, str);
    }

    public void ReplicateUserData( int stringIndex, StringTableUserData userData, in CRecipientFilter filter )
    {
        using var msg = netMessageService.Create<CSVCMsg_UpdateStringTable>();
        msg.TableId = TableId;
        msg.NumChangedEntries = 1;
        msg.Recipients = filter;

        unsafe
        {
            fixed (byte* userDataPtr = userData.Data)
            {
                msg.StringData = NativeStringTable.Serialize(handle, stringIndex, "", false, (nint)userDataPtr, userData.Data.Length);
            }
        }

        msg.Send();
    }
    public void ReplicateUserData( string str, StringTableUserData userData, in CRecipientFilter filter )
    {
        if (FindStringIndex(str) is not { } index)
        {
            throw new ArgumentException("Failed to replicate string user data. String is not found in string table.");
        }
        ReplicateUserData(index, userData, filter);
    }


}