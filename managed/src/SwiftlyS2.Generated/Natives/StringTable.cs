#pragma warning disable CS0649
#pragma warning disable CS0169

using System.Buffers;
using System.Text;
using System.Threading;
using SwiftlyS2.Shared.Natives;

namespace SwiftlyS2.Core.Natives;

internal static class NativeStringTable
{

    private unsafe static delegate* unmanaged<byte*, nint> _ContainerFindTable;

    public unsafe static nint ContainerFindTable(string tableName)
    {
        var pool = ArrayPool<byte>.Shared;
        var tableNameLength = Encoding.UTF8.GetByteCount(tableName);
        var tableNameBuffer = pool.Rent(tableNameLength + 1);
        Encoding.UTF8.GetBytes(tableName, tableNameBuffer);
        tableNameBuffer[tableNameLength] = 0;
        fixed (byte* tableNameBufferPtr = tableNameBuffer)
        {
            var ret = _ContainerFindTable(tableNameBufferPtr);
            pool.Return(tableNameBuffer);
            return ret;
        }
    }

    private unsafe static delegate* unmanaged<int, nint> _ContainerGetTableById;

    public unsafe static nint ContainerGetTableById(int tableId)
    {
        var ret = _ContainerGetTableById(tableId);
        return ret;
    }

    private unsafe static delegate* unmanaged<nint, int> _GetTableId;

    public unsafe static int GetTableId(nint table)
    {
        var ret = _GetTableId(table);
        return ret;
    }

    private unsafe static delegate* unmanaged<byte*, nint, int> _GetTableName;

    public unsafe static string GetTableName(nint table)
    {
        var ret = _GetTableName(null, table);
        var pool = ArrayPool<byte>.Shared;
        var retBuffer = pool.Rent(ret + 1);
        fixed (byte* retBufferPtr = retBuffer)
        {
            ret = _GetTableName(retBufferPtr, table);
            var retString = Encoding.UTF8.GetString(retBufferPtr, ret);
            pool.Return(retBuffer);
            return retString;
        }
    }

    private unsafe static delegate* unmanaged<nint, int> _GetNumStrings;

    public unsafe static int GetNumStrings(nint table)
    {
        var ret = _GetNumStrings(table);
        return ret;
    }

    private unsafe static delegate* unmanaged<nint, byte*, int> _FindStringIndex;

    public unsafe static int FindStringIndex(nint table, string str)
    {
        var pool = ArrayPool<byte>.Shared;
        var strLength = Encoding.UTF8.GetByteCount(str);
        var strBuffer = pool.Rent(strLength + 1);
        Encoding.UTF8.GetBytes(str, strBuffer);
        strBuffer[strLength] = 0;
        fixed (byte* strBufferPtr = strBuffer)
        {
            var ret = _FindStringIndex(table, strBufferPtr);
            pool.Return(strBuffer);
            return ret;
        }
    }

    private unsafe static delegate* unmanaged<nint, int, byte> _IsStringIndexValid;

    public unsafe static bool IsStringIndexValid(nint table, int index)
    {
        var ret = _IsStringIndexValid(table, index);
        return ret == 1;
    }

    private unsafe static delegate* unmanaged<byte*, nint, int, int> _GetString;

    public unsafe static string GetString(nint table, int index)
    {
        var ret = _GetString(null, table, index);
        var pool = ArrayPool<byte>.Shared;
        var retBuffer = pool.Rent(ret + 1);
        fixed (byte* retBufferPtr = retBuffer)
        {
            ret = _GetString(retBufferPtr, table, index);
            var retString = Encoding.UTF8.GetString(retBufferPtr, ret);
            pool.Return(retBuffer);
            return retString;
        }
    }

    private unsafe static delegate* unmanaged<nint, int, nint> _GetStringUserData;

    public unsafe static nint GetStringUserData(nint table, int index)
    {
        var ret = _GetStringUserData(table, index);
        return ret;
    }

    private unsafe static delegate* unmanaged<nint, int, nint, int, byte, byte> _SetStringUserData;

    public unsafe static bool SetStringUserData(nint table, int index, nint userData, int userDataSize, bool forceOverride)
    {
        var ret = _SetStringUserData(table, index, userData, userDataSize, forceOverride ? (byte)1 : (byte)0);
        return ret == 1;
    }

    private unsafe static delegate* unmanaged<nint, byte*, int> _AddString;

    public unsafe static int AddString(nint table, string str)
    {
        var pool = ArrayPool<byte>.Shared;
        var strLength = Encoding.UTF8.GetByteCount(str);
        var strBuffer = pool.Rent(strLength + 1);
        Encoding.UTF8.GetBytes(str, strBuffer);
        strBuffer[strLength] = 0;
        fixed (byte* strBufferPtr = strBuffer)
        {
            var ret = _AddString(table, strBufferPtr);
            pool.Return(strBuffer);
            return ret;
        }
    }

    private unsafe static delegate* unmanaged<byte*, nint, int, byte*, byte, nint, int, int> _Serialize;

    public unsafe static byte[] Serialize(nint table, int index, string keyName, bool newKey, nint userData, int userDataSize)
    {
        var pool = ArrayPool<byte>.Shared;
        var keyNameLength = Encoding.UTF8.GetByteCount(keyName);
        var keyNameBuffer = pool.Rent(keyNameLength + 1);
        Encoding.UTF8.GetBytes(keyName, keyNameBuffer);
        keyNameBuffer[keyNameLength] = 0;
        fixed (byte* keyNameBufferPtr = keyNameBuffer)
        {
            var ret = _Serialize(null, table, index, keyNameBufferPtr, newKey ? (byte)1 : (byte)0, userData, userDataSize);
            var retBuffer = pool.Rent(ret + 1);
            fixed (byte* retBufferPtr = retBuffer)
            {
                ret = _Serialize(retBufferPtr, table, index, keyNameBufferPtr, newKey ? (byte)1 : (byte)0, userData, userDataSize);
                var retBytes = new byte[ret];
                for (int i = 0; i < ret; i++) retBytes[i] = retBufferPtr[i];
                pool.Return(retBuffer);
                pool.Return(keyNameBuffer);
                return retBytes;
            }
        }
    }
}