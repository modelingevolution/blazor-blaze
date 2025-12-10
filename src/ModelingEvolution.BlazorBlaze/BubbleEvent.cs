namespace ModelingEvolution.BlazorBlaze;

public record BubbleEvent<TOwner, TPayload>(Action<TOwner, TOwner, TPayload> On) : IBubbleEvent<TPayload>
{
    public Type OwnerType => typeof(TOwner);
    public Type PayloadType => typeof(TPayload);
    public void Raise(object target, object owner, TPayload payload)
    {
        if (target is Control c) c.OnRaise(this,  owner, payload);
        if(target is TOwner t && owner is TOwner o)
            On(t,o, payload);
    }

    void IBubbleEvent.InvokeDelegate(Delegate On, object owner, object payload)
    {
        var o = (Action<TOwner, TPayload>)On;
        o((TOwner)owner, (TPayload)payload);
    }
}