using System.Collections.Concurrent;
using System.Collections.Immutable;
using BlazorBlaze.ValueTypes;

namespace BlazorBlaze.Tests;

/// <summary>
/// Tests for Ref, RefArray, and Lease with thread-safety verification.
/// </summary>
public class RefTests
{
    private class MockBitmap : IDisposable
    {
        private int _disposeCount;

        public int Id { get; }
        public bool IsDisposed => _disposeCount > 0;
        public int DisposeCount => _disposeCount;

        public MockBitmap(int id) => Id = id;

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    #region Helper Methods

    private static RefArray<T> CreateRefArray<T>(params Ref<T>?[] refs) where T : IDisposable
    {
        var builder = ImmutableArray.CreateBuilder<Ref<T>?>(refs.Length);
        foreach (var r in refs)
            builder.Add(r);
        return new RefArray<T>(builder.MoveToImmutable());
    }

    /// <summary>
    /// Disposes a nullable RefArray safely using GetValueOrDefault to avoid
    /// shadowing issues with the internal Value property.
    /// </summary>
    private static void DisposeArray<T>(ref RefArray<T>? array) where T : IDisposable
    {
        if (array.HasValue)
        {
            var arr = array.GetValueOrDefault();
            arr.Dispose();
            array = null;
        }
    }

    /// <summary>
    /// Gets value at index from nullable RefArray.
    /// </summary>
    private static T? GetValue<T>(RefArray<T>? array, int index) where T : IDisposable
    {
        if (!array.HasValue) return default;
        var arr = array.GetValueOrDefault();
        return arr[index];
    }

    #endregion

    #region Ref Basic Tests

    [Fact]
    public void Ref_Create_StartsWithRefCount1()
    {
        var bitmap = new MockBitmap(1);
        var r = new Ref<MockBitmap>(bitmap);

        r.RefCount.Should().Be(1);
        r.Value.Should().BeSameAs(bitmap);
    }

    [Fact]
    public void Ref_TryCopy_IncrementsRefCount()
    {
        var bitmap = new MockBitmap(1);
        var r1 = new Ref<MockBitmap>(bitmap);

        r1.TryCopy(out var r2).Should().BeTrue();

        r2.Should().BeSameAs(r1);
        r1.RefCount.Should().Be(2);
    }

    [Fact]
    public void Ref_Dispose_DecrementsRefCount()
    {
        var bitmap = new MockBitmap(1);
        var r = new Ref<MockBitmap>(bitmap);
        r.TryCopy(out _);

        r.Dispose();

        r.RefCount.Should().Be(1);
        bitmap.IsDisposed.Should().BeFalse("still has one reference");
    }

    [Fact]
    public void Ref_DisposeLast_DisposesValue()
    {
        var bitmap = new MockBitmap(1);
        var r = new Ref<MockBitmap>(bitmap);

        r.Dispose();

        bitmap.IsDisposed.Should().BeTrue("ref count reached 0");
    }

    [Fact]
    public void Ref_TryCopy_ReturnsFalseAfterDispose()
    {
        var bitmap = new MockBitmap(1);
        var r = new Ref<MockBitmap>(bitmap);

        r.Dispose();

        r.TryCopy(out var copy).Should().BeFalse("value was disposed");
        copy.Should().BeNull();
    }

    #endregion

    #region RefArray Basic Tests (Immutable Design)

    [Fact]
    public void RefArray_TryCopy_CreatesIndependentCopy()
    {
        var bitmap = new MockBitmap(1);
        var originalRef = new Ref<MockBitmap>(bitmap);
        var array1 = CreateRefArray(originalRef);

        array1.TryCopy(out var array2).Should().BeTrue();

        array2.Should().NotBeNull();
        GetValue(array2, 0).Should().BeSameAs(bitmap);
        originalRef.RefCount.Should().Be(2);

        array1.Dispose();
        bitmap.IsDisposed.Should().BeFalse("array2 still holds reference");

        DisposeArray(ref array2);
        bitmap.IsDisposed.Should().BeTrue("all references released");
    }

    [Fact]
    public void RefArray_Dispose_ReleasesAllSlots()
    {
        var bitmaps = Enumerable.Range(0, 5).Select(i => new MockBitmap(i)).ToList();
        var refs = bitmaps.Select(b => new Ref<MockBitmap>(b)).Cast<Ref<MockBitmap>?>().ToArray();
        var array = CreateRefArray(refs);

        array.Dispose();

        bitmaps.Should().OnlyContain(b => b.IsDisposed);
    }

    [Fact]
    public void RefArray_TryCopy_ReturnsFalseAfterDispose()
    {
        var bitmap = new MockBitmap(0);
        var array = CreateRefArray(new Ref<MockBitmap>(bitmap));

        array.Dispose();

        array.TryCopy(out var copy).Should().BeFalse();
        copy.Should().BeNull();
    }

    [Fact]
    public void RefArray_Dispose_IsIdempotent()
    {
        var bitmap = new MockBitmap(0);
        var array = CreateRefArray(new Ref<MockBitmap>(bitmap));

        array.Dispose();
        array.Dispose();
        array.Dispose();

        bitmap.DisposeCount.Should().Be(1, "disposed exactly once");
    }

    [Fact]
    public void RefArray_GetRef_ReturnsUnderlyingRef()
    {
        var bitmap = new MockBitmap(1);
        var originalRef = new Ref<MockBitmap>(bitmap);
        var array = CreateRefArray(originalRef);

        var retrievedRef = array.GetRef(0);

        retrievedRef.Should().BeSameAs(originalRef);
        array.Dispose();
    }

    [Fact]
    public void RefArray_Indexer_ReturnsValue()
    {
        var bitmap = new MockBitmap(1);
        var array = CreateRefArray(new Ref<MockBitmap>(bitmap));

        var value = array[0];

        value.Should().BeSameAs(bitmap);
        array.Dispose();
    }

    [Fact]
    public void RefArray_WithNullSlots_HandlesCorrectly()
    {
        var bitmap = new MockBitmap(1);
        var array = CreateRefArray<MockBitmap>(null, new Ref<MockBitmap>(bitmap), null);

        array.GetRef(0).Should().BeNull();
        array.GetRef(1).Should().NotBeNull();
        array.GetRef(2).Should().BeNull();
        array[1].Should().BeSameAs(bitmap);

        array.Dispose();
        bitmap.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Ref_TryCopy_ThreadSafety()
    {
        const int iterations = 1000;
        var errors = new List<string>();

        for (int i = 0; i < iterations; i++)
        {
            var bitmap = new MockBitmap(i);
            var r = new Ref<MockBitmap>(bitmap);

            var barrier = new Barrier(2);
            Ref<MockBitmap>? copy = null;
            var copySuccess = false;

            var copyTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                copySuccess = r.TryCopy(out copy);
            });

            var disposeTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                r.Dispose();
            });

            await Task.WhenAll(copyTask, disposeTask);

            if (copySuccess && copy != null)
            {
                if (copy.RefCount <= 0)
                    errors.Add($"Iteration {i}: TryCopy succeeded but RefCount is {copy.RefCount}");
                copy.Dispose();
            }

            bitmap.DisposeCount.Should().Be(1, $"iteration {i}");
        }

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RefArray_TryCopy_ThreadSafety_ConcurrentCopyAndDispose()
    {
        const int iterations = 500;
        const int copyThreads = 4;
        var errors = new List<string>();

        for (int iter = 0; iter < iterations; iter++)
        {
            var bitmaps = Enumerable.Range(0, 4).Select(j => new MockBitmap(iter * 100 + j)).ToList();
            var refs = bitmaps.Select(b => new Ref<MockBitmap>(b)).Cast<Ref<MockBitmap>?>().ToArray();
            var array = CreateRefArray(refs);

            var barrier = new Barrier(copyThreads + 1);
            var copies = new RefArray<MockBitmap>?[copyThreads];
            var results = new bool[copyThreads];

            var tasks = new Task[copyThreads + 1];

            for (int t = 0; t < copyThreads; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    results[threadIndex] = array.TryCopy(out copies[threadIndex]);
                });
            }

            tasks[copyThreads] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                array.Dispose();
            });

            await Task.WhenAll(tasks);

            for (int t = 0; t < copyThreads; t++)
            {
                if (results[t])
                {
                    var copy = copies[t];
                    if (copy == null)
                    {
                        errors.Add($"Iter {iter}, Thread {t}: TryCopy true but copy is null");
                        continue;
                    }

                    // Check that all 4 slots have values
                    for (int j = 0; j < bitmaps.Count; j++)
                    {
                        var value = GetValue(copy, j);
                        if (value == null)
                            errors.Add($"Iter {iter}, Thread {t}: Layer {j} value is null");
                        else if (value.Id != bitmaps[j].Id)
                            errors.Add($"Iter {iter}, Thread {t}: Layer {j} wrong value");
                    }

                    DisposeArray(ref copy);
                    copies[t] = null;
                }
            }

            foreach (var bitmap in bitmaps)
            {
                if (bitmap.DisposeCount != 1)
                    errors.Add($"Iter {iter}: Bitmap {bitmap.Id} disposed {bitmap.DisposeCount} times");
            }
        }

        errors.Should().BeEmpty(string.Join("\n", errors.Take(10)));
    }

    #endregion

    #region Lease Tests

    [Fact]
    public void Lease_Dispose_ReturnsToPool()
    {
        var bitmap = new MockBitmap(1);
        MockBitmap? returnedItem = null;

        var lease = new Lease<MockBitmap>(bitmap, item => returnedItem = item);

        lease.Value.Should().BeSameAs(bitmap);
        lease.IsDisposed.Should().BeFalse();

        lease.Dispose();

        lease.IsDisposed.Should().BeTrue();
        returnedItem.Should().BeSameAs(bitmap);
        bitmap.IsDisposed.Should().BeFalse("lease returns to pool, doesn't dispose");
    }

    [Fact]
    public void Lease_Dispose_IsIdempotent()
    {
        var bitmap = new MockBitmap(1);
        var returnCount = 0;

        var lease = new Lease<MockBitmap>(bitmap, _ => returnCount++);

        lease.Dispose();
        lease.Dispose();
        lease.Dispose();

        returnCount.Should().Be(1, "should only return once");
    }

    [Fact]
    public void Lease_WithRef_RefCountingWorks()
    {
        var bitmap = new MockBitmap(1);
        MockBitmap? returnedItem = null;

        var lease = new Lease<MockBitmap>(bitmap, item => returnedItem = item);
        var r = new Ref<Lease<MockBitmap>>(lease);

        r.TryCopy(out var r2).Should().BeTrue();
        r.RefCount.Should().Be(2);

        r.Dispose();
        
        returnedItem.Should().BeNull("still has one reference");
        lease.IsDisposed.Should().BeFalse();

        r2!.Dispose();
        returnedItem.Should().BeSameAs(bitmap, "last ref released, returned to pool");
        
        lease.IsDisposed.Should().BeFalse(); // this lease had been not disposed.
        r2.Value.IsDisposed.Should().BeTrue("lease disposed");

    }

    [Fact]
    public void Lease_WithRefArray_FrameSnapshotPattern()
    {
        var bitmaps = new List<MockBitmap>();
        var returnedItems = new List<MockBitmap>();

        // Simulate pool
        MockBitmap RentBitmap(int id)
        {
            var b = new MockBitmap(id);
            bitmaps.Add(b);
            return b;
        }

        void ReturnBitmap(MockBitmap b) => returnedItems.Add(b);

        // Frame 1: Create snapshot with 3 layers
        var layer0 = new Ref<Lease<MockBitmap>>(new Lease<MockBitmap>(RentBitmap(0), ReturnBitmap));
        var layer1 = new Ref<Lease<MockBitmap>>(new Lease<MockBitmap>(RentBitmap(1), ReturnBitmap));
        var layer2 = new Ref<Lease<MockBitmap>>(new Lease<MockBitmap>(RentBitmap(2), ReturnBitmap));

        var builder1 = ImmutableArray.CreateBuilder<Ref<Lease<MockBitmap>>?>(3);
        builder1.Add(layer0);
        builder1.Add(layer1);
        builder1.Add(layer2);
        var frame1 = new RefArray<Lease<MockBitmap>>(builder1.MoveToImmutable());

        // Frame 2: Layer 0 changes, Layer 1 remains, Layer 2 removed
        var newLayer0 = new Ref<Lease<MockBitmap>>(new Lease<MockBitmap>(RentBitmap(10), ReturnBitmap));
        layer1.TryCopy(out var layer1Copy);  // Remain pattern - increment ref count

        var builder2 = ImmutableArray.CreateBuilder<Ref<Lease<MockBitmap>>?>(2);
        builder2.Add(newLayer0);
        builder2.Add(layer1Copy);
        var frame2 = new RefArray<Lease<MockBitmap>>(builder2.MoveToImmutable());

        // Dispose frame1
        frame1.Dispose();

        // Layer 0 (id=0) should be returned - no refs
        // Layer 1 (id=1) should NOT be returned - frame2 still holds ref
        // Layer 2 (id=2) should be returned - no refs
        returnedItems.Should().HaveCount(2);
        returnedItems.Should().Contain(b => b.Id == 0);
        returnedItems.Should().Contain(b => b.Id == 2);
        returnedItems.Should().NotContain(b => b.Id == 1);

        // Dispose frame2
        frame2.Dispose();

        // Now all should be returned
        returnedItems.Should().HaveCount(4);
        returnedItems.Select(b => b.Id).Should().BeEquivalentTo([0, 2, 10, 1]);

        // None of the bitmaps should be disposed (pool can reuse them)
        bitmaps.Should().OnlyContain(b => !b.IsDisposed);
    }

    #endregion
}
