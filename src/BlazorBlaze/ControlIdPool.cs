namespace BlazorBlaze;

class ControlIdPool
{
    private readonly Stack<uint> _reuse = new();
    private uint _nextId = 1; //0 is reserved for root;
    
    public uint Rent()
    {
        if (_reuse.Count > 0) return _reuse.Pop();

        return (uint)_nextId++;
    }

    public void Return(uint id)
    {
        _reuse.Push(id);
    }
    
}