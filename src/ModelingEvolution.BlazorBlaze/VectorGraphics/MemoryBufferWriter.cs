using System.Buffers;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics
{
    public class MemoryBufferWriter : IBufferWriter<byte>
    {
        private Memory<byte> _memory;
        private int _writtenMemory;
        public int WrittenMemory => _writtenMemory;
        public MemoryBufferWriter(ref Memory<byte> memory)
        {
            _memory = memory;
            _writtenMemory = 0;
        }

        public MemoryBufferWriter Init(ref Memory<byte> memory)
        {
            _memory = memory;
            _writtenMemory = 0;
            return this;
        }
        public void Advance(int count)
        {
            if (count < 0 || _writtenMemory + count > _memory.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            _writtenMemory += count;
        }
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }
            if (_writtenMemory + sizeHint > _memory.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer.");
            }
            return _memory.Slice(_writtenMemory);
        }
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return GetMemory(sizeHint).Span;
        }
    }
}
