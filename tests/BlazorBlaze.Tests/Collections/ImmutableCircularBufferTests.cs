using BlazorBlaze.Collections;

namespace ModelingEvolution.BlazorBlaze.Tests.Collections;

public class ImmutableCircularBufferTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesEmptyBuffer()
    {
        var buffer = new ImmutableCircularBuffer<int>(10);

        buffer.Count.Should().Be(0);
        buffer.Capacity.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidCapacity_Throws(int capacity)
    {
        var act = () => new ImmutableCircularBuffer<int>(capacity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SingleItem_ReturnsNewBufferWithItem()
    {
        var buffer = new ImmutableCircularBuffer<int>(5);

        var newBuffer = buffer.Add(42);

        newBuffer.Count.Should().Be(1);
        newBuffer[0].Should().Be(42);
        newBuffer.Last.Should().Be(42);

        // Original unchanged (immutability)
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Add_MultipleItems_MaintainsOrder()
    {
        var buffer = new ImmutableCircularBuffer<int>(5)
            .Add(1)
            .Add(2)
            .Add(3);

        buffer.Count.Should().Be(3);
        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);
        buffer[2].Should().Be(3);
        buffer.Last.Should().Be(3);
    }

    [Fact]
    public void Add_BeyondCapacity_RemovesOldestItem()
    {
        var buffer = new ImmutableCircularBuffer<int>(3)
            .Add(1)
            .Add(2)
            .Add(3)
            .Add(4); // exceeds capacity

        buffer.Count.Should().Be(3);
        buffer[0].Should().Be(2); // oldest (1) removed
        buffer[1].Should().Be(3);
        buffer[2].Should().Be(4);
        buffer.Last.Should().Be(4);
    }

    [Fact]
    public void Add_WayBeyondCapacity_KeepsOnlyRecentItems()
    {
        var buffer = new ImmutableCircularBuffer<int>(3);

        for (int i = 1; i <= 100; i++)
            buffer = buffer.Add(i);

        buffer.Count.Should().Be(3);
        buffer[0].Should().Be(98);
        buffer[1].Should().Be(99);
        buffer[2].Should().Be(100);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var buffer = new ImmutableCircularBuffer<int>(5).Add(1).Add(2);

        var actNegative = () => buffer[-1];
        var actTooHigh = () => buffer[2];

        actNegative.Should().Throw<IndexOutOfRangeException>();
        actTooHigh.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void Last_OnEmptyBuffer_Throws()
    {
        var buffer = new ImmutableCircularBuffer<int>(5);

        var act = () => buffer.Last;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Enumeration_ReturnsItemsInOrder()
    {
        var buffer = new ImmutableCircularBuffer<int>(5)
            .Add(10)
            .Add(20)
            .Add(30);

        buffer.Should().BeEquivalentTo(new[] { 10, 20, 30 }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Enumeration_AfterWraparound_ReturnsCorrectOrder()
    {
        var buffer = new ImmutableCircularBuffer<int>(3)
            .Add(1).Add(2).Add(3).Add(4).Add(5);

        buffer.Should().BeEquivalentTo(new[] { 3, 4, 5 }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Immutability_OriginalBufferUnchanged()
    {
        var original = new ImmutableCircularBuffer<string>(3).Add("a").Add("b");
        var modified = original.Add("c");

        original.Count.Should().Be(2);
        modified.Count.Should().Be(3);

        original[0].Should().Be("a");
        original[1].Should().Be("b");
    }

    [Fact]
    public void WithReferenceTypes_WorksCorrectly()
    {
        var buffer = new ImmutableCircularBuffer<string>(2)
            .Add("first")
            .Add("second")
            .Add("third");

        buffer.Count.Should().Be(2);
        buffer[0].Should().Be("second");
        buffer[1].Should().Be("third");
        buffer.Last.Should().Be("third");
    }
}
