using SoundGuard.Core.Audio;
using Xunit;

namespace SoundGuard.Tests;

public class LockFreeRingBufferTests
{
    [Fact]
    public void RoundTrip_PreservesData()
    {
        var ring = new LockFreeRingBuffer(64);
        float[] src = { 1f, 2f, 3f, 4f, 5f };

        Assert.True(ring.TryWrite(src, 0, 5));
        Assert.Equal(5, ring.Available);

        var dst = new float[5];
        Assert.Equal(5, ring.Read(dst, 0, 5));
        for (int i = 0; i < 5; i++)
            Assert.Equal(src[i], dst[i]);

        Assert.Equal(0, ring.Available);
    }

    [Fact]
    public void Overflow_DropsWholeBlockAndCountsFrames()
    {
        var ring = new LockFreeRingBuffer(20); // rounds to 32 floats of capacity
        var src = new float[40];

        Assert.False(ring.TryWrite(src, 0, 40));
        Assert.Equal(40L, ring.DroppedFrames);
        Assert.Equal(0, ring.Available);
    }

    [Fact]
    public void Capacity_IsPowerOfTwo()
    {
        var ring = new LockFreeRingBuffer(100);
        int capacity = ring.CapacityFloats;
        Assert.True((capacity & (capacity - 1)) == 0, $"Capacity {capacity} should be a power of two");
    }

    [Fact]
    public void PartialRead_LeavesRemainderAvailable()
    {
        var ring = new LockFreeRingBuffer(64);
        var src = new float[10];
        for (int i = 0; i < 10; i++) src[i] = i;

        Assert.True(ring.TryWrite(src, 0, 10));

        var dst = new float[6];
        Assert.Equal(6, ring.Read(dst, 0, 6));
        Assert.Equal(4, ring.Available);
    }
}
