namespace BlazorBlaze;

public abstract class ControlExtension<TControl> : IControlExtension
{
    public TControl Control { get; private set; }
    public BlazeEngine Engine { get; private set; }
    public abstract void Bind();
    public abstract void Unbind();
    void IControlExtension.Bind(Control control, BlazeEngine engine)
    {
        if (control is TControl typedControl)
        {
            this.Control  = typedControl;
            this.Engine = engine;
            Bind();
        }
        else
            throw new InvalidOperationException($"Control is not of type {typeof(TControl).Name}");
    }

    void IControlExtension.Unbind(Control control, BlazeEngine engine)
    {
        if (control is TControl typedControl && typedControl.Equals(this.Control))
            Unbind();
        else
            throw new InvalidOperationException($"Control is not of type {typeof(TControl).Name}");
    }
}