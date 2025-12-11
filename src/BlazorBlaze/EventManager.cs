using System.Threading.Channels;
using Microsoft.AspNetCore.Components.Web;

using WebMouseEventArgs = Microsoft.AspNetCore.Components.Web.MouseEventArgs;
using WebWheelEventArgs = Microsoft.AspNetCore.Components.Web.WheelEventArgs;
using WebKeyboardEventArgs = Microsoft.AspNetCore.Components.Web.KeyboardEventArgs;

namespace BlazorBlaze;

// we have 2 systems:
// Event-Subsystem - custom. 
// And Property-Subsystem - based on ReactiveUI
public class EventManager
{
    private readonly Channel<Action> _actionChannel;
    private readonly Channel<IRootEvent> _generalEventsChannel;
    private readonly Channel<IRootEvent> _mouseMoveChannel;
    private readonly Action<WebMouseEventArgs, IBubbleEvent<MouseEventArgs>> _onMouse;
    private readonly Action<WheelEventArgs, IBubbleEvent<WheelMouseEventArgs>> _onWheel;
    private readonly Action<WebKeyboardEventArgs, IBubbleEvent<WebKeyboardEventArgs>> _onKey;


    public EventManager(Action<WebMouseEventArgs, IBubbleEvent<MouseEventArgs>> onMouse, 
        Action<WheelEventArgs, IBubbleEvent<WheelMouseEventArgs>> onWheel, 
        Action<WebKeyboardEventArgs, IBubbleEvent<WebKeyboardEventArgs>> onKey)
    {
        _onMouse = onMouse;
        _onWheel = onWheel;
        _onKey = onKey;
        _mouseMoveChannel = Channel.CreateBounded<IRootEvent>(new BoundedChannelOptions(1)
            { FullMode = BoundedChannelFullMode.DropOldest });
        _generalEventsChannel = Channel.CreateUnbounded<IRootEvent>();
        _actionChannel = Channel.CreateUnbounded<Action>();
    }

    public void Queue(WebKeyboardEventArgs args, IBubbleEvent<WebKeyboardEventArgs> evt)
    {
        _generalEventsChannel.Writer.TryWrite(
            new RootEvent<WebKeyboardEventArgs, WebKeyboardEventArgs>(static (x, a, e) => HandleEvent(x, a, e), args, evt));
    }
   
    public void QueueBounded(WebMouseEventArgs args, IBubbleEvent<MouseEventArgs> evt) =>
        _mouseMoveChannel.Writer.TryWrite(
            new RootEvent<WebMouseEventArgs, MouseEventArgs>(static (x, a, e) => HandleEvent(x, a, e), args, evt));

    public void Queue(WebMouseEventArgs args, IBubbleEvent<MouseEventArgs> evt) =>
        _generalEventsChannel.Writer.TryWrite(
            new RootEvent<WebMouseEventArgs, MouseEventArgs>(static (x, a, e) => HandleEvent(x, a, e), args, evt));
    public void Queue(WheelEventArgs args, IBubbleEvent<WheelMouseEventArgs> evt) =>
        _generalEventsChannel.Writer.TryWrite(
            new RootEvent<WheelEventArgs, WheelMouseEventArgs>(static (x, a, e) => HandleEvent(x, a, e), args, evt));

    public void QueueAction(Action action) => _actionChannel.Writer.TryWrite(action);

    private static void HandleEvent(EventManager mgm, WebMouseEventArgs mouseEventArgs, IBubbleEvent<MouseEventArgs> evt) => mgm._onMouse(mouseEventArgs, evt);
    private static void HandleEvent(EventManager mgm, WebKeyboardEventArgs mouseEventArgs, IBubbleEvent<WebKeyboardEventArgs> evt) => mgm._onKey(mouseEventArgs, evt);
    private static void HandleEvent(EventManager mgm, WheelEventArgs mouseEventArgs, IBubbleEvent<WheelMouseEventArgs> evt) => mgm._onWheel(mouseEventArgs, evt);


    public void DoEvents()
    {
        // Process MouseMove events
        if (_mouseMoveChannel.Reader.TryRead(out var moveEvent))
            moveEvent.Fire(this);

        // Process all available general events
        while (_generalEventsChannel.Reader.TryRead(out var eventArgs))
            eventArgs.Fire(this);

        while (_actionChannel.Reader.TryRead(out var action))
            action();
    }
}