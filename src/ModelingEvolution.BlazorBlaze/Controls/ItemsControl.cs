using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ModelingEvolution.BlazorBlaze;

public abstract class ItemsControl : Control
{
    public ObservableCollection<Control> Children { get; } = new();

    protected ItemsControl()
    {
        Children.CollectionChanged += OnCollectionChanged;
        base.ObservableZIndex().Subscribe(z =>
        {
            foreach (var i in Children.Where(x => x.ZIndex == 0))
                i.ZIndex = z;
        });
    }

 

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            // This is invoked when Children collection changes. When we add for instance, we would set all child control's parents to this. 
            // We would set Parent to null on remove
            case NotifyCollectionChangedAction.Add:
                foreach (Control child in e.NewItems)
                {
                    child.Parent = this;
                    if (child.ZIndex == 0 && this.ZIndex != 0) child.ZIndex = this.ZIndex;
                    if(this.Engine != null)
                        this.Engine.Scene.AddControl(child);
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (Control child in e.OldItems)
                {
                    child.Parent = null;
                    if (this.Engine != null)
                        this.Engine.Scene.RemoveControl(child);
                }

                break;
            case NotifyCollectionChangedAction.Reset:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Move: break;
            case NotifyCollectionChangedAction.Replace:
                foreach (Control child in e.OldItems)
                {
                    child.Parent = null;
                    if (this.Engine != null)
                        this.Engine.Scene.RemoveControl(child);
                }

                foreach (Control child in e.NewItems)
                {
                    child.Parent = this;
                    if (this.Engine != null)
                        this.Engine.Scene.AddControl(child);
                }
                break;

        }
    }

    protected override void Dispose(bool disposing)
    {
        foreach(var i in this.Children)
            i.Dispose();
    }
}