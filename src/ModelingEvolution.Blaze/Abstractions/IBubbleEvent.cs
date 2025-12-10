namespace ModelingEvolution.Blaze;

public interface IBubbleEvent
{
    Type OwnerType { get; }
    Type PayloadType { get; }
    void InvokeDelegate(Delegate On, object owner, object payload);
}