namespace ModelingEvolution.Blaze;

internal readonly record struct RootEvent<T, TR>(
    Action<EventManager, T, IBubbleEvent<TR>> On,
    T Arg,
    IBubbleEvent<TR> Event) : IRootEvent
{
    void IRootEvent.Fire(EventManager manager)
    {
        On(manager, Arg, Event);
    }
}