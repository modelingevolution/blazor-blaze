namespace ModelingEvolution.Blaze;

public class MouseOverControlChangedArgs : System.EventArgs
{
    public Control Previous { get; init; }
    public Control Current { get; init; }
}