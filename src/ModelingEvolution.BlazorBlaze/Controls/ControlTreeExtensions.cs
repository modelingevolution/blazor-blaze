namespace ModelingEvolution.BlazorBlaze;

public static class ControlTreeExtensions
{
    public static IEnumerable<Control> Tree(this Control c)
    {
        using var stack = new ManagedArray<Control>(128);

        stack.Add(c);
        while (stack.Count > 0)
        {
            var current = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            yield return current;

            if(current is ItemsControl it)
                foreach (Control child in it.Children) stack.Add(child);

            else if(current is ContentControl cc && cc.Content != null)
                stack.Add(cc.Content);
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