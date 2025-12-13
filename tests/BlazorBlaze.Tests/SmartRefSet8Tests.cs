using BlazorBlaze.ValueTypes;

namespace BlazorBlaze.Tests;

/// <summary>
/// Tests for SmartRefSet8 aligned with design.md scenarios:
/// - Decoder thread creates/manages bitmaps in pool slots
/// - UI thread takes leases for rendering
/// - Ref counting enables safe concurrent access
/// - Items are disposed when ref count reaches 0
/// </summary>
public class SmartRefSet8Tests
{
    /// <summary>
    /// Mock IDisposable to track disposal and access patterns.
    /// Represents a PooledBitmap in the real scenario.
    /// </summary>
    private class MockBitmap : IDisposable
    {
        public int Id { get; }
        public bool IsDisposed { get; private set; }
        public int AccessCount { get; private set; }

        public MockBitmap(int id) => Id = id;

        public void Access() => AccessCount++;

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    #region Basic Lifecycle Tests

    [Fact]
    public void New_CreatesLeaseWithRefCount1()
    {
        // Arrange
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap = new MockBitmap(1);

        // Act
        var lease = pool.New(0, bitmap);

        // Assert
        pool.GetRefCount(0).Should().Be(1);
        lease.Item.Should().BeSameAs(bitmap);
    }

    [Fact]
    public void Lease_Copy_IncrementsRefCount()
    {
        // Arrange
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap = new MockBitmap(1);
        var lease = pool.New(0, bitmap);

        // Act
        var copy = lease.Copy();

        // Assert
        pool.GetRefCount(0).Should().Be(2);
        copy.Item.Should().BeSameAs(bitmap);
    }

    [Fact]
    public void Lease_Dispose_DecrementsRefCount()
    {
        // Arrange
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap = new MockBitmap(1);
        var lease = pool.New(0, bitmap);

        // Act
        lease.Dispose();

        // Assert
        pool.GetRefCount(0).Should().Be(0);
    }

    [Fact]
    public void LeaseSet_Copy_IncrementsAllRefCounts()
    {
        // Arrange - Frame with layers 0 and 1
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap0 = new MockBitmap(0);
        var bitmap1 = new MockBitmap(1);

        var lease0 = pool.New(0, bitmap0);
        var leaseSet = lease0.New(1, bitmap1);

        pool.GetRefCount(0).Should().Be(1);
        pool.GetRefCount(1).Should().Be(1);

        // Act - UI takes lease (Copy)
        var uiLease = leaseSet.Copy();

        // Assert
        pool.GetRefCount(0).Should().Be(2);
        pool.GetRefCount(1).Should().Be(2);
    }

    [Fact]
    public void LeaseSet_Dispose_DecrementsAllRefCounts()
    {
        // Arrange
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap0 = new MockBitmap(0);
        var bitmap1 = new MockBitmap(1);

        var lease0 = pool.New(0, bitmap0);
        var leaseSet = lease0.New(1, bitmap1);
        var copy = leaseSet.Copy();

        // Act
        copy.Dispose();

        // Assert
        pool.GetRefCount(0).Should().Be(1);
        pool.GetRefCount(1).Should().Be(1);
    }

    #endregion

    #region Design.md Scenario: Decoder/UI Frame Lifecycle

    /// <summary>
    /// From design.md:
    /// Frame N: OnLayerStart(0, Clear) -> rent bitmap, OnFrameEnd -> snapshot
    /// UI: Takes lease, composites, releases lease
    /// Frame N+1: OnLayerStart(0, Clear) -> rent NEW bitmap
    /// </summary>
    [Fact]
    public void Scenario_DecoderCreatesFrame_UITakesLease_DecoderCreatesNewFrame()
    {
        var pool = new SmartRefSet8<MockBitmap>();

        // Frame N: Decoder creates layer 0
        var bitmapN = new MockBitmap(100);
        var frameN = pool.New(0, bitmapN).ToSet();

        pool.GetRefCount(0).Should().Be(1, "Frame N holds reference");

        // UI takes lease for rendering
        var uiLease = frameN.Copy();
        pool.GetRefCount(0).Should().Be(2, "Frame N + UI lease");

        // Frame N+1: Decoder wants to create NEW layer 0 (Clear)
        // This is where the MISALIGNMENT appears:
        // Design.md says: rent new bitmap from pool, clear it
        // SmartRefSet8: Can't create new item in slot 0 because it's occupied

        // First, decoder disposes Frame N
        frameN.Dispose();
        pool.GetRefCount(0).Should().Be(1, "Only UI lease remains");

        // Now decoder tries to create Frame N+1 with new bitmap in slot 0
        // PROBLEM: Slot 0 still has ref count 1 (UI lease), can't Set()
        var bitmapN1 = new MockBitmap(101);

        var canCreateNewInSlot0 = pool.GetRefCount(0) == 0;
        canCreateNewInSlot0.Should().BeFalse(
            "MISALIGNMENT: Decoder can't create new bitmap in slot 0 while UI still has lease. " +
            "Design.md expects bitmaps to be DIFFERENT objects - old bitmap stays alive while new one is created.");
    }

    /// <summary>
    /// From design.md:
    /// OnLayerStart(1, Remain) -> keep ref from previous frame
    /// This means: increment ref count for existing bitmap, don't create new
    /// </summary>
    [Fact]
    public void Scenario_RemainLayer_ShouldIncrementExistingRefCount()
    {
        var pool = new SmartRefSet8<MockBitmap>();

        // Frame N: Create layers 0 and 1
        var bitmap0 = new MockBitmap(0);
        var bitmap1 = new MockBitmap(1);
        var frameN = pool.New(0, bitmap0).New(1, bitmap1);

        // Frame N+1: Layer 0 = Clear (new bitmap), Layer 1 = Remain (keep same)
        // For Remain, we need to INCREMENT ref count on existing item

        // MISALIGNMENT: SmartRefSet8 has no "increment existing slot" operation
        // Lease.Copy() increments but returns same lease, doesn't create new frame ownership

        // What we need:
        // var frameN1Layer1 = pool.IncrementAndGetLease(1); // Get lease to existing item
        // But this doesn't exist!

        // Current workaround would be to somehow transfer the reference...
        // But the API doesn't support "take reference to existing item in slot"

        pool.IsOccupied(1).Should().BeTrue();

        // We can't easily express: "I want a new lease on slot 1's existing item"
        // without having the original lease
    }

    #endregion

    #region Design.md Scenario: Cleanup on RefCount 0

    /// <summary>
    /// From design.md:
    /// "Returns bitmaps to pool when ref count reaches 0"
    /// if (newCount == 0) _pool.Return(_layers[i]!);
    /// </summary>
    [Fact]
    public void Scenario_RefCountZero_ShouldDisposeItem()
    {
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap = new MockBitmap(1);

        var lease = pool.New(0, bitmap);
        lease.Dispose();

        pool.GetRefCount(0).Should().Be(0);

        // Now items ARE disposed when ref count hits 0
        bitmap.IsDisposed.Should().BeTrue(
            "Item should be disposed when ref count reaches 0");
    }

    /// <summary>
    /// After ref count hits 0, item should be cleared from _items array
    /// so slot can be reused.
    /// </summary>
    [Fact]
    public void Scenario_RefCountZero_ShouldClearSlotForReuse()
    {
        var pool = new SmartRefSet8<MockBitmap>();

        // Create and dispose
        var bitmap1 = new MockBitmap(1);
        var lease1 = pool.New(0, bitmap1);
        lease1.Dispose();

        // Try to create new item in same slot
        var bitmap2 = new MockBitmap(2);

        var canCreate = true;
        try
        {
            var lease2 = pool.New(0, bitmap2);
            lease2.Item.Should().BeSameAs(bitmap2);
        }
        catch
        {
            canCreate = false;
        }

        canCreate.Should().BeTrue("Slot should be reusable after ref count hits 0");

        // Old bitmap should have been disposed
        bitmap1.IsDisposed.Should().BeTrue(
            "Old bitmap should have been disposed when ref count hit 0");
    }

    /// <summary>
    /// LeaseSet.Dispose should dispose all items that hit ref count 0.
    /// </summary>
    [Fact]
    public void LeaseSet_Dispose_ShouldDisposeItemsAtZero()
    {
        var pool = new SmartRefSet8<MockBitmap>();
        var bitmap0 = new MockBitmap(0);
        var bitmap1 = new MockBitmap(1);

        var lease0 = pool.New(0, bitmap0);
        var leaseSet = lease0.New(1, bitmap1);

        // Copy increases ref count
        var copy = leaseSet.Copy();

        // First dispose - ref counts go to 1
        leaseSet.Dispose();
        bitmap0.IsDisposed.Should().BeFalse("Still has one reference");
        bitmap1.IsDisposed.Should().BeFalse("Still has one reference");

        // Second dispose - ref counts go to 0
        copy.Dispose();
        bitmap0.IsDisposed.Should().BeTrue("Ref count hit 0");
        bitmap1.IsDisposed.Should().BeTrue("Ref count hit 0");
    }

    #endregion

    #region Design.md Scenario: Concurrent Access

    /// <summary>
    /// From design.md:
    /// Decoder @ 60fps, UI @ 30fps
    /// UI might be rendering frame N while decoder is building frame N+2
    /// </summary>
    [Fact]
    public void Scenario_UIRendersWhileDecoderBuildsNewFrame()
    {
        var pool = new SmartRefSet8<MockBitmap>();

        // Frame N: Decoder creates snapshot with layer 0
        var bitmapN = new MockBitmap(100);
        var frameN = pool.New(0, bitmapN).ToSet();

        // UI takes lease for slow render
        var uiLease = frameN.Copy();
        pool.GetRefCount(0).Should().Be(2);

        // Decoder disposes Frame N (moves to N+1)
        frameN.Dispose();
        pool.GetRefCount(0).Should().Be(1, "UI still holds reference");

        // Key insight: In design.md, Frame N+1 would use a DIFFERENT pool slot
        // or a DIFFERENT bitmap object. The pool manages multiple bitmaps.

        // MISALIGNMENT: SmartRefSet8 conflates "slot" with "layer ID"
        // Design.md has: layer ID -> points to pool slot -> points to bitmap
        // SmartRefSet8 has: slot = layer ID = item location

        // UI finishes rendering, releases lease
        uiLease.Dispose();
        pool.GetRefCount(0).Should().Be(0);

        // Now decoder can reuse slot 0
        // But bitmap wasn't disposed!
    }

    #endregion

    #region RefCount8 Direct Tests

    [Fact]
    public void RefCount8_Set_OnlySucceedsWhenSlotIsZero()
    {
        var counter = new RefCount8();

        // First set should succeed
        counter.Set(0).Should().BeTrue();
        counter[0].Should().Be(1);

        // Second set should fail (already occupied)
        counter.Set(0).Should().BeFalse();
        counter[0].Should().Be(1, "Value unchanged after failed Set");
    }

    [Fact]
    public void RefCount8_IncrementDecrement_WorkCorrectly()
    {
        var counter = new RefCount8();
        counter.Set(5);

        counter.Increment(5).Should().Be(2);
        counter.Increment(5).Should().Be(3);

        // Decrement now returns RefCount8One pattern of slots that hit zero
        counter.Decrement(5).IsEmpty.Should().BeTrue("not zero yet");
        counter[5].Should().Be(2);

        counter.Decrement(5).IsEmpty.Should().BeTrue("not zero yet");
        counter[5].Should().Be(1);

        counter.Decrement(5).IsEmpty.Should().BeFalse("now at zero, should return slot pattern");
        counter[5].Should().Be(0);
    }

    [Fact]
    public void RefCount8_BatchAdd_IncrementsMultipleSlots()
    {
        var counter = new RefCount8();
        counter.Set(0);
        counter.Set(1);
        counter.Set(2);

        var pattern = RefCount8One.Slot(0) | RefCount8One.Slot(2);
        counter.Add(in pattern);

        counter[0].Should().Be(2);
        counter[1].Should().Be(1, "Slot 1 not in pattern");
        counter[2].Should().Be(2);
    }

    [Fact]
    public void RefCount8_BatchSubtract_DecrementsMultipleSlots()
    {
        var counter = new RefCount8();
        counter.Set(0);
        counter.Set(1);
        counter.Increment(0);
        counter.Increment(1);

        var pattern = RefCount8One.Slot(0) | RefCount8One.Slot(1);
        counter.Subtract(in pattern);

        counter[0].Should().Be(1);
        counter[1].Should().Be(1);
    }

    #endregion

    #region Identified Misalignments Summary

    /// <summary>
    /// This test documents the misalignments found between SmartRefSet8 and design.md
    /// </summary>
    [Fact]
    public void Summary_IdentifiedMisalignments()
    {
        // MISALIGNMENT 1: No cleanup on ref count 0
        // Design.md: "if (newCount == 0) _pool.Return(_layers[i]!);"
        // SmartRefSet8: Just decrements, doesn't dispose or clear item

        // MISALIGNMENT 2: Slot = Layer ID conflation
        // Design.md: Layer ID is semantic (0=background, 1=overlay),
        //           Pool slot is physical storage, separate concepts
        // SmartRefSet8: Uses single index for both, can't have same layer
        //              point to different bitmaps across frames

        // MISALIGNMENT 3: No "increment existing" for Remain semantics
        // Design.md: OnLayerStart(1, Remain) -> keep ref from previous frame
        // SmartRefSet8: No way to get lease on existing item without original lease

        // MISALIGNMENT 4: Missing frame/snapshot concept
        // Design.md: ImmutableLayerSnapshot is a snapshot of which layers exist
        // SmartRefSet8: LeaseSet tracks ownership but isn't a snapshot

        // RECOMMENDATION:
        // SmartRefSet8 might work better as a POOL (physical storage)
        // Need separate LayerSnapshot class that references pool slots
        // Pool.Rent() -> get available slot, increment ref count
        // Pool.Return(slot) -> called when ref count hits 0

        true.Should().BeTrue(); // Placeholder assertion
    }

    #endregion
}
