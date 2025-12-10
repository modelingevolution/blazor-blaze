using System.Reactive.Subjects;

namespace ModelingEvolution.BlazorBlaze;





public class ObservableProperty<TOwner,T> : IDisposable
{
    public readonly record struct Args(TOwner Sender, T Previous, T Current)
    {
        public static implicit operator T(Args a) => a.Current;
    }
    private readonly Subject<Args> _subject = new Subject<Args>();
    private T _currentValue;

    public ObservableProperty(T initialValue)
    {
        _currentValue = initialValue;
    }

    public static implicit operator T(ObservableProperty<TOwner,T> value)
    {
        return value.Value;
    }
    
    public void Change(TOwner sender, T value)
    {
        if (_currentValue != null && _currentValue.Equals(value)) return;
        if (_currentValue == null && value == null) return;
        var prv = _currentValue;
        _currentValue = value;
        _subject.OnNext(new Args(sender, prv, _currentValue)); // Notify subscribers of the change
    }
    public T Value => _currentValue;

    public IObservable<Args> AsObservable() => _subject;

    public void Dispose() => _subject.Dispose();
}
