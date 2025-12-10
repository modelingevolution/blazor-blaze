namespace ModelingEvolution.Blaze;

public class Disposable : IDisposable
{
    private readonly Action _onDispose;
    public Disposable(Action onDispose)
    {
        _onDispose = onDispose;
    }
    public void Dispose()
    {
        _onDispose();
    }
}