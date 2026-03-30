using System.Text.Json.Serialization;
using BlazorBlaze.Scene3D.Geometries;
using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D;

/// <summary>
/// A mutable node in the scene graph tree. Each node has a local transform (Pose3),
/// optional geometry, optional material properties, and children.
/// </summary>
public sealed class SceneNode
{
    private readonly List<SceneNode> _children = new();

    /// <summary>
    /// Unique name of this node within its parent's children.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Local transform relative to the parent node.
    /// </summary>
    public Pose3<double> LocalPose { get; set; } = Pose3<double>.Identity;

    /// <summary>
    /// Optional geometry attached to this node.
    /// </summary>
    public IGeometry? Geometry { get; set; }

    /// <summary>
    /// Node color. Defaults to white (opaque).
    /// </summary>
    public Color Color { get; set; } = Color.FromRgb(255, 255, 255);

    /// <summary>
    /// Opacity from 0.0 (transparent) to 1.0 (opaque).
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Whether this node (and its children) are visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// The parent node, or null if this is a root-level node.
    /// </summary>
    [JsonIgnore]
    public SceneNode? Parent { get; internal set; }

    /// <summary>
    /// The scene graph this node belongs to (set for root nodes, inherited via parent chain).
    /// </summary>
    [JsonIgnore]
    internal SceneGraph? Graph { get; set; }

    /// <summary>
    /// Read-only access to child nodes.
    /// </summary>
    public IReadOnlyList<SceneNode> Children => _children;

    public SceneNode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Adds a child node. Throws if a child with the same name already exists.
    /// </summary>
    public SceneNode AddChild(string name)
    {
        if (_children.Exists(c => c.Name == name))
            throw new ArgumentException($"A child node named '{name}' already exists under '{Name}'.");

        var child = new SceneNode(name) { Parent = this };
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Attaches an existing node as a child of this node. The node is detached
    /// from its current parent (or from the scene graph roots) first.
    /// Throws if a child with the same name already exists or if attaching would create a cycle.
    /// </summary>
    public void AttachChild(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node == this)
            throw new InvalidOperationException("Cannot attach a node to itself.");

        if (_children.Exists(c => c.Name == node.Name))
            throw new ArgumentException($"A child node named '{node.Name}' already exists under '{Name}'.");

        // Cycle detection: walk up from this node; if we hit 'node', it's an ancestor.
        var current = this;
        while (current is not null)
        {
            if (current == node)
                throw new InvalidOperationException(
                    $"Cannot attach '{node.Name}' as child of '{Name}' because it is an ancestor of this node.");
            current = current.Parent;
        }

        // Detach from current location.
        node.DetachFromCurrentParent();

        node.Parent = this;
        _children.Add(node);
    }

    /// <summary>
    /// Removes a child node by name. Returns true if found and removed.
    /// Also removes all descendants recursively.
    /// </summary>
    public bool RemoveChild(string name)
    {
        var index = _children.FindIndex(c => c.Name == name);
        if (index < 0) return false;

        var child = _children[index];
        child.Parent = null;
        child.DetachAll();
        _children.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Finds a descendant node by name using depth-first search.
    /// Returns null if not found.
    /// </summary>
    public SceneNode? Find(string name)
    {
        if (Name == name) return this;

        foreach (var child in _children)
        {
            var found = child.Find(name);
            if (found is not null) return found;
        }

        return null;
    }

    /// <summary>
    /// Returns the depth of this node (number of ancestors including itself).
    /// Root node depth is 1.
    /// </summary>
    public int Depth()
    {
        var count = 1;
        var current = Parent;
        while (current is not null)
        {
            count++;
            current = current.Parent;
        }
        return count;
    }

    /// <summary>
    /// Detaches this node from its current parent or from the scene graph roots.
    /// </summary>
    internal void DetachFromCurrentParent()
    {
        if (Parent is not null)
        {
            Parent._children.Remove(this);
            Parent = null;
        }
        else if (Graph is not null)
        {
            Graph.RemoveRootInternal(this);
            Graph = null;
        }
    }

    private void DetachAll()
    {
        foreach (var child in _children)
        {
            child.Parent = null;
            child.DetachAll();
        }
        _children.Clear();
    }
}
