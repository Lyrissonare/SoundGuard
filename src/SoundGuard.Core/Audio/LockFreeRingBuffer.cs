using System.Threading;

namespace SoundGuard.Core.Audio;

/// <summary>
/// Single-producer / single-consumer (SPSC) lock-free ring buffer for interleaved float samples.
///
/// The capture callback is the sole producer and the analysis thread is the sole consumer, so no
/// lock or CAS is needed for the payload — only volatile head/tail publication for visibility.
/// Positions are plain <c>int</c>; under C#'s default unchecked arithmetic <c>write - read</c> wraps
/// correctly as long as the live window is far below 2^31 samples (it is), so no overflow handling
/// is required. Capacity is rounded up to a power of two so indexing is a single AND.
/// </summary>
public sealed class LockFreeRingBuffer
{
    private readonly float[] _data;
    private readonly int _mask;

    private int _readPos;   // consumer-only
    private int _writePos;  // producer-only
    private long _droppedFrames;

    public LockFreeRingBuffer(int capacityFloats)
    {
        _data = new float[NextPowerOfTwo(Math.Max(16, capacityFloats))];
        _mask = _data.Length - 1;
    }

    /// <summary>Number of frames (not samples) dropped because the buffer was full.</summary>
    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

    public int CapacityFloats => _data.Length;

    /// <summary>Number of floats currently buffered and readable.</summary>
    public int Available => Volatile.Read(ref _writePos) - Volatile.Read(ref _readPos);

    /// <summary>Copy <paramref name="count"/> floats from <paramref name="src"/> into the buffer. Non-blocking.</summary>
    public bool TryWrite(float[] src, int offset, int count)
    {
        int read = Volatile.Read(ref _readPos);
        int write = Volatile.Read(ref _writePos);

        if (_data.Length - (write - read) < count)
        {
            // Drop the whole block rather than interleave partial frames: keeps channel/frame
            // alignment intact for the analyzer.
            Interlocked.Add(ref _droppedFrames, count);
            return false;
        }

        for (int i = 0; i < count; i++)
            _data[(write + i) & _mask] = src[offset + i];

        Volatile.Write(ref _writePos, write + count);
        return true;
    }

    /// <summary>Copy up to <paramref name="maxCount"/> floats into <paramref name="dest"/>. Returns the count read.</summary>
    public int Read(float[] dest, int offset, int maxCount)
    {
        int read = Volatile.Read(ref _readPos);
        int write = Volatile.Read(ref _writePos);
        int available = write - read;
        int count = Math.Min(available, maxCount);

        for (int i = 0; i < count; i++)
            dest[offset + i] = _data[(read + i) & _mask];

        if (count > 0)
            Volatile.Write(ref _readPos, read + count);

        return count;
    }

    private static int NextPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
