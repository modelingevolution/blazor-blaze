using ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;

namespace ModelingEvolution.BlazorBlaze.Tests.VectorGraphics.Protocol;

public class BinaryEncodingTests
{
    #region Varint Tests

    [Theory]
    [InlineData(0u, new byte[] { 0x00 })]
    [InlineData(1u, new byte[] { 0x01 })]
    [InlineData(127u, new byte[] { 0x7F })]
    [InlineData(128u, new byte[] { 0x80, 0x01 })]
    [InlineData(255u, new byte[] { 0xFF, 0x01 })]
    [InlineData(300u, new byte[] { 0xAC, 0x02 })]
    [InlineData(16383u, new byte[] { 0xFF, 0x7F })]
    [InlineData(16384u, new byte[] { 0x80, 0x80, 0x01 })]
    public void WriteVarint_UInt32_EncodesCorrectly(uint value, byte[] expected)
    {
        var buffer = new byte[5];
        int bytesWritten = BinaryEncoding.WriteVarint(buffer, value);

        bytesWritten.Should().Be(expected.Length);
        buffer.Take(bytesWritten).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0u)]
    [InlineData(new byte[] { 0x01 }, 1u)]
    [InlineData(new byte[] { 0x7F }, 127u)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128u)]
    [InlineData(new byte[] { 0xFF, 0x01 }, 255u)]
    [InlineData(new byte[] { 0xAC, 0x02 }, 300u)]
    [InlineData(new byte[] { 0xFF, 0x7F }, 16383u)]
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16384u)]
    public void ReadVarint_UInt32_DecodesCorrectly(byte[] data, uint expected)
    {
        int bytesConsumed = BinaryEncoding.ReadVarint(data, out uint value);

        bytesConsumed.Should().Be(data.Length);
        value.Should().Be(expected);
    }

    [Fact]
    public void ReadVarint_IncompleteData_ReturnsZero()
    {
        var incompleteData = new byte[] { 0x80 }; // Incomplete - continuation bit set but no following byte

        int bytesConsumed = BinaryEncoding.ReadVarint(incompleteData, out uint value);

        bytesConsumed.Should().Be(0);
        value.Should().Be(0);
    }

    [Fact]
    public void WriteVarint_ULong_HandlesLargeValues()
    {
        ulong largeValue = ulong.MaxValue >> 1; // Large but valid
        var buffer = new byte[10];

        int bytesWritten = BinaryEncoding.WriteVarint(buffer, largeValue);
        int bytesConsumed = BinaryEncoding.ReadVarint(buffer, out ulong decoded);

        bytesConsumed.Should().Be(bytesWritten);
        decoded.Should().Be(largeValue);
    }

    [Fact]
    public void WriteReadVarint_RoundTrip_PreservesValue()
    {
        var testValues = new uint[] { 0, 1, 127, 128, 255, 256, 16383, 16384, 65535, 1000000, uint.MaxValue };
        var buffer = new byte[5];

        foreach (var original in testValues)
        {
            int written = BinaryEncoding.WriteVarint(buffer, original);
            int consumed = BinaryEncoding.ReadVarint(buffer, out uint decoded);

            consumed.Should().Be(written, $"for value {original}");
            decoded.Should().Be(original);
        }
    }

    #endregion

    #region ZigZag Tests

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(-1, 1u)]
    [InlineData(1, 2u)]
    [InlineData(-2, 3u)]
    [InlineData(2, 4u)]
    [InlineData(-64, 127u)]
    [InlineData(64, 128u)]
    [InlineData(int.MaxValue, 4294967294u)]
    [InlineData(int.MinValue, 4294967295u)]
    public void ZigZagEncode_Int32_EncodesCorrectly(int value, uint expected)
    {
        uint encoded = BinaryEncoding.ZigZagEncode(value);
        encoded.Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, -1)]
    [InlineData(2u, 1)]
    [InlineData(3u, -2)]
    [InlineData(4u, 2)]
    [InlineData(127u, -64)]
    [InlineData(128u, 64)]
    [InlineData(4294967294u, int.MaxValue)]
    [InlineData(4294967295u, int.MinValue)]
    public void ZigZagDecode_UInt32_DecodesCorrectly(uint encoded, int expected)
    {
        int decoded = BinaryEncoding.ZigZagDecode(encoded);
        decoded.Should().Be(expected);
    }

    [Fact]
    public void ZigZag_RoundTrip_PreservesValue()
    {
        var testValues = new int[] { 0, 1, -1, 100, -100, 1000, -1000, int.MaxValue, int.MinValue };

        foreach (var original in testValues)
        {
            uint encoded = BinaryEncoding.ZigZagEncode(original);
            int decoded = BinaryEncoding.ZigZagDecode(encoded);
            decoded.Should().Be(original);
        }
    }

    [Fact]
    public void ZigZag_Int64_RoundTrip()
    {
        var testValues = new long[] { 0, 1, -1, 100, -100, long.MaxValue / 2, long.MinValue / 2 };

        foreach (var original in testValues)
        {
            ulong encoded = BinaryEncoding.ZigZagEncode(original);
            long decoded = BinaryEncoding.ZigZagDecode(encoded);
            decoded.Should().Be(original);
        }
    }

    #endregion

    #region SignedVarint Tests

    [Fact]
    public void WriteSignedVarint_SmallPositive_CompactEncoding()
    {
        var buffer = new byte[5];
        int bytesWritten = BinaryEncoding.WriteSignedVarint(buffer, 1);

        // 1 -> zigzag(1) = 2 -> varint(2) = 0x02 (1 byte)
        bytesWritten.Should().Be(1);
        buffer[0].Should().Be(0x02);
    }

    [Fact]
    public void WriteSignedVarint_SmallNegative_CompactEncoding()
    {
        var buffer = new byte[5];
        int bytesWritten = BinaryEncoding.WriteSignedVarint(buffer, -1);

        // -1 -> zigzag(-1) = 1 -> varint(1) = 0x01 (1 byte)
        bytesWritten.Should().Be(1);
        buffer[0].Should().Be(0x01);
    }

    [Fact]
    public void WriteReadSignedVarint_RoundTrip()
    {
        var testValues = new int[] { 0, 1, -1, 63, -64, 64, -65, 1000, -1000, int.MaxValue, int.MinValue };
        var buffer = new byte[10];

        foreach (var original in testValues)
        {
            int written = BinaryEncoding.WriteSignedVarint(buffer, original);
            int consumed = BinaryEncoding.ReadSignedVarint(buffer, out int decoded);

            consumed.Should().Be(written, $"for value {original}");
            decoded.Should().Be(original);
        }
    }

    [Fact]
    public void ReadSignedVarint_IncompleteData_ReturnsZero()
    {
        var incompleteData = new byte[] { 0x80 };

        int bytesConsumed = BinaryEncoding.ReadSignedVarint(incompleteData, out int value);

        bytesConsumed.Should().Be(0);
        value.Should().Be(0);
    }

    #endregion

    #region VarintSize Tests

    [Theory]
    [InlineData(0UL, 1)]
    [InlineData(127UL, 1)]
    [InlineData(128UL, 2)]
    [InlineData(16383UL, 2)]
    [InlineData(16384UL, 3)]
    [InlineData(2097151UL, 3)]
    [InlineData(2097152UL, 4)]
    public void VarintSize_ReturnsCorrectByteCount(ulong value, int expectedSize)
    {
        int size = BinaryEncoding.VarintSize(value);
        size.Should().Be(expectedSize);
    }

    [Fact]
    public void SignedVarintSize_SmallValues_Compact()
    {
        // Small values (both positive and negative) should be compact
        BinaryEncoding.SignedVarintSize(0).Should().Be(1);
        BinaryEncoding.SignedVarintSize(1).Should().Be(1);
        BinaryEncoding.SignedVarintSize(-1).Should().Be(1);
        BinaryEncoding.SignedVarintSize(63).Should().Be(1);
        BinaryEncoding.SignedVarintSize(-64).Should().Be(1);
        BinaryEncoding.SignedVarintSize(64).Should().Be(2);
        BinaryEncoding.SignedVarintSize(-65).Should().Be(2);
    }

    #endregion

    #region Delta Encoding Simulation

    [Fact]
    public void DeltaEncoding_SequentialPoints_CompactRepresentation()
    {
        // Simulate encoding sequential points with small deltas
        var points = new (int X, int Y)[]
        {
            (100, 100),
            (102, 101), // delta: +2, +1
            (105, 102), // delta: +3, +1
            (108, 100), // delta: +3, -2
        };

        var buffer = new byte[100];
        int offset = 0;

        // First point: absolute (zigzag + varint)
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].X);
        offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), points[0].Y);

        int firstPointSize = offset;

        // Subsequent points: deltas
        int lastX = points[0].X;
        int lastY = points[0].Y;

        for (int i = 1; i < points.Length; i++)
        {
            int deltaX = points[i].X - lastX;
            int deltaY = points[i].Y - lastY;

            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), deltaX);
            offset += BinaryEncoding.WriteSignedVarint(buffer.AsSpan(offset), deltaY);

            lastX = points[i].X;
            lastY = points[i].Y;
        }

        // Verify we can decode back
        int readOffset = 0;
        var decoded = new List<(int X, int Y)>();

        readOffset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(readOffset), out int x);
        readOffset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(readOffset), out int y);
        decoded.Add((x, y));

        for (int i = 1; i < points.Length; i++)
        {
            readOffset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(readOffset), out int dx);
            readOffset += BinaryEncoding.ReadSignedVarint(buffer.AsSpan(readOffset), out int dy);
            x += dx;
            y += dy;
            decoded.Add((x, y));
        }

        decoded.Should().BeEquivalentTo(points);

        // Delta encoding should be more compact (small deltas = small varints)
        int deltasSize = offset - firstPointSize;
        deltasSize.Should().BeLessThan(firstPointSize * (points.Length - 1),
            "Delta encoding should be more compact than absolute encoding");
    }

    #endregion
}
