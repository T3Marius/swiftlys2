using System.Collections.Concurrent;
using System.Text;
using SwiftlyS2.Core.Natives;

namespace SwiftlyS2.Core.Natives;

internal class StringPool
{
    private static readonly ConcurrentDictionary<string, nint> _cache = new();

    private static readonly Lock _poolLock = new();
    private static nint _currentBlock = nint.Zero;
    private static int _remainingBytes = 0;

    private const int BLOCK_SIZE = 8192;

    public static nint Allocate( string str )
    {
        if (_cache.TryGetValue(str, out var addr))
        {
            return addr;
        }

        lock (_poolLock)
        {
            if (_cache.TryGetValue(str, out addr))
            {
                return addr;
            }

            var byteCount = Encoding.UTF8.GetByteCount(str);
            var neededSize = byteCount + 1; 

            if (neededSize > BLOCK_SIZE / 2)
            {
                addr = NativeAllocator.Alloc((ulong)neededSize);
                WriteBytes(addr, str, byteCount);
                _cache[str] = addr;
                return addr;
            }

            if (_remainingBytes < neededSize)
            {
                _currentBlock = NativeAllocator.Alloc(BLOCK_SIZE);
                _remainingBytes = BLOCK_SIZE;
            }

            addr = _currentBlock;

            WriteBytes(addr, str, byteCount);

            _currentBlock += neededSize;
            _remainingBytes -= neededSize;

            _cache[str] = addr;
            return addr;
        }
    }

    private static unsafe void WriteBytes( nint addr, string str, int length )
    {
        var span = new Span<byte>(addr.ToPointer(), length + 1);
        Encoding.UTF8.GetBytes(str, span);
        span[length] = 0;
    }
}