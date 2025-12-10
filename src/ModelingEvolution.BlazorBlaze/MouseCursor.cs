namespace ModelingEvolution.BlazorBlaze;

public record MouseCursor<TOwner>(TOwner Owner) : IDisposable
{
    public IDisposable Change(MouseCursorType type)
    {
        var tmp = Type;
        
        Type = type;
        Console.WriteLine($"Will rollback to {tmp}");
        return new Disposable(() =>
        {
            Type = tmp;
            Console.WriteLine($"Rolling back to {tmp}");
        });
    }


    private readonly ObservableProperty<TOwner, MouseCursorType> _type = new(MouseCursorType.Default);
    public MouseCursorType Type
    {
        get => _type.Value;
        set => _type.Change(this.Owner, value);
    }

    public IObservable<ObservableProperty<TOwner, MouseCursorType>.Args> ObservableType()
    {
        return _type.AsObservable();
    }

    public void Dispose()
    {
        _type.Dispose();
    }
}
