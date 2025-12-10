namespace ModelingEvolution.Blaze;

public static class ControlTreeExtensions
{
    public static IEnumerable<Control> Tree(this Control c)
    {
        var stack = new Stack<Control>(128);

        stack.Push(c);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if(current is ItemsControl it)
                foreach (Control child in it.Children) stack.Push(child);

            else if(current is ContentControl cc && cc.Content != null)
                stack.Push(cc.Content);
        }
    }
    public static void PropagateEvent<T>(this Control owner, IBubbleEvent<T> evt, T tmp) //where T: IMouseEventArgs
    {
        evt.Raise(owner, owner, tmp);

        var current = owner;
        while (current.Parent != null)
        {
            evt.Raise(current.Parent, owner, tmp);
            current = current.Parent;
        }
    }
}