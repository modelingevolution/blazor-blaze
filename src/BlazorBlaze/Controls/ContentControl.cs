namespace BlazorBlaze;

public abstract class ContentControl : Control
{
    public Control Content { get; protected internal set; }
}