using System.Buffers;
using System.Runtime.CompilerServices;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics.Protocol;

/// <summary>
/// Provides varint and zigzag encoding/decoding utilities for efficient binary data transfer.
/// Compatible with Protocol Buffers varint encoding.
/// </summary>
public static class BinaryEncoding
{
    /// <summary>
    /// Encodes an unsigned integer using variable-length encoding (varint).
    /// Small values use fewer bytes (1 byte for 0-127, 2 bytes for 128-16383, etc.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteVarint(Span<byte> buffer, ulong value)
    {
        int i = 0;
        while (value >= 0x80)
        {
            buffer[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[i++] = (byte)value;
        return i;
    }

    /// <summary>
    /// Encodes an unsigned 32-bit integer using variable-length encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteVarint(Span<byte> buffer, uint value)
    {
        int i = 0;
        while (value >= 0x80)
        {
            buffer[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[i++] = (byte)value;
        return i;
    }

    /// <summary>
    /// Decodes a variable-length encoded unsigned 64-bit integer.
    /// </summary>
    /// <returns>Number of bytes consumed, or 0 if more data is needed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadVarint(ReadOnlySpan<byte> buffer, out ulong value)
    {
        value = 0;
        int shift = 0;
        int i = 0;

        while (i < buffer.Length)
        {
            byte b = buffer[i++];
            value |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return i;

            shift += 7;
            if (shift >= 64)
                throw new InvalidOperationException("Varint overflow");
        }

        // Need more data
        value = 0;
        return 0;
    }

    /// <summary>
    /// Decodes a variable-length encoded unsigned 32-bit integer.
    /// </summary>
    /// <returns>Number of bytes consumed, or 0 if more data is needed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadVarint(ReadOnlySpan<byte> buffer, out uint value)
    {
        value = 0;
        int shift = 0;
        int i = 0;

        while (i < buffer.Length)
        {
            byte b = buffer[i++];
            value |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return i;

            shift += 7;
            if (shift >= 35)
                throw new InvalidOperationException("Varint overflow");
        }

        // Need more data
        value = 0;
        return 0;
    }

    /// <summary>
    /// Encodes a signed integer using zigzag encoding for efficient varint representation.
    /// Maps signed values to unsigned: 0 -> 0, -1 -> 1, 1 -> 2, -2 -> 3, 2 -> 4, etc.
    /// This makes small negative values small positive values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ZigZagEncode(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }

    /// <summary>
    /// Decodes a zigzag-encoded unsigned value back to signed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ZigZagDecode(uint value)
    {
        return (int)((value >> 1) ^ -(int)(value & 1));
    }

    /// <summary>
    /// Encodes a signed 64-bit integer using zigzag encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ZigZagEncode(long value)
    {
        return (ulong)((value << 1) ^ (value >> 63));
    }

    /// <summary>
    /// Decodes a zigzag-encoded unsigned 64-bit value back to signed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ZigZagDecode(ulong value)
    {
        return (long)(value >> 1) ^ -(long)(value & 1);
    }

    /// <summary>
    /// Writes a signed integer using combined zigzag + varint encoding.
    /// This is optimal for signed values where magnitude is typically small.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedVarint(Span<byte> buffer, int value)
    {
        return WriteVarint(buffer, ZigZagEncode(value));
    }

    /// <summary>
    /// Reads a signed integer using combined zigzag + varint decoding.
    /// </summary>
    /// <returns>Number of bytes consumed, or 0 if more data is needed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadSignedVarint(ReadOnlySpan<byte> buffer, out int value)
    {
        int consumed = ReadVarint(buffer, out uint unsigned);
        value = consumed > 0 ? ZigZagDecode(unsigned) : 0;
        return consumed;
    }

    /// <summary>
    /// Writes a signed 64-bit integer using combined zigzag + varint encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSignedVarint(Span<byte> buffer, long value)
    {
        return WriteVarint(buffer, ZigZagEncode(value));
    }

    /// <summary>
    /// Reads a signed 64-bit integer using combined zigzag + varint decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadSignedVarint(ReadOnlySpan<byte> buffer, out long value)
    {
        int consumed = ReadVarint(buffer, out ulong unsigned);
        value = consumed > 0 ? ZigZagDecode(unsigned) : 0;
        return consumed;
    }

    /// <summary>
    /// Calculates the number of bytes needed to encode a value as varint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VarintSize(ulong value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }
        return size;
    }

    /// <summary>
    /// Calculates the number of bytes needed to encode a signed value using zigzag + varint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SignedVarintSize(int value)
    {
        return VarintSize(ZigZagEncode(value));
    }
}
