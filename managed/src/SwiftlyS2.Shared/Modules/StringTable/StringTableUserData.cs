using System.Text;

namespace SwiftlyS2.Shared.StringTable;
public readonly ref struct StringTableUserData
{
    internal ReadOnlySpan<byte> Data { get; }
    private StringTableUserData( ReadOnlySpan<byte> data ) { Data = data; }

    public static StringTableUserData FromString( string str ) => new(Encoding.UTF8.GetBytes(str + "\0"));

    public static StringTableUserData FromRaw( byte[] data ) => new(data);

    public static StringTableUserData FromRaw( ReadOnlySpan<byte> data ) => new(data);
}

public readonly ref struct StringTableOutUserData
{
    internal ReadOnlySpan<byte> Data { get; }
    public bool IsValid { get; }
    internal StringTableOutUserData( ReadOnlySpan<byte> data, bool isValid ) { Data = data; IsValid = isValid; }
    private void ThrowIfInvalid() { if (!IsValid) throw new InvalidOperationException("User data is invalid."); }
    public string AsString() { ThrowIfInvalid(); return Encoding.UTF8.GetString(Data); }
    public ReadOnlySpan<byte> AsRaw() { ThrowIfInvalid(); return Data; }

}