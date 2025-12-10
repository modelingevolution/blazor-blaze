namespace ModelingEvolution.BlazorBlaze;

public interface IBubbleEvent<in TPayload> : IBubbleEvent
{
    public void Raise(object target, object owner, TPayload payload);
    
}