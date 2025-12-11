using System.Buffers;

namespace BlazorBlaze.VectorGraphics;

public class SlidingBufferWriter : IBufferWriter<byte>
{
    private readonly byte[] _buffer = new byte[8 * 1024 * 1024];
    // The amount of data that should be only occupied by one slide. 
    private readonly int _maxChunk = 32 * 1024;
    private int _written;
    private int _offset;
    public int WrittenBytes => _written;
    public int BytesLeft => _buffer.Length - _offset- _written;
    // Slide 
    public void SlideChunk()
    {
        _offset += _written;
        _written = 0;
        if (BytesLeft < _maxChunk)
        {
            _offset = 0;
            //Console.WriteLine("Slide chunk!");
        }
    }
    public void Write(byte[] buffer, int offset, int count)
    {
        int index = _written + _offset;
        if (index + count > _buffer.Length)
        {
            throw new IndexOutOfRangeException();
        }
        else
        {
            Buffer.BlockCopy(buffer, offset, _buffer, index, count);
        }
    }

    public void Advance(int count)
    {
        _written += count;
    }

    //private PeriodicConsoleWriter w = new PeriodicConsoleWriter(TimeSpan.FromSeconds(10));
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));

        if (BytesLeft < sizeHint) SlideChunk();

        //w.WriteLine($"Bytes left in buffer: {BytesLeft}");
        
        return _buffer.AsMemory(_offset + _written);
    }

    public Memory<byte> WrittenMemory => _buffer.AsMemory(_offset, _written);

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));

        if (BytesLeft < sizeHint) SlideChunk();

        return _buffer.AsSpan(_offset + _written);
    }
}