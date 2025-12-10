namespace ModelingEvolution.BlazorBlaze;

public interface IBubbleEvent
{
    Type OwnerType { get; }
    Type PayloadType { get; }
    void InvokeDelegate(Delegate On, object owner, object payload);
}