namespace BlazorBlaze.Scene3D;

/// <summary>
/// A mutable tree of SceneNode objects representing a 3D scene.
/// Agents can programmatically build, inspect, and modify this graph.
/// This is the stable contract -- rendering adapters translate it to engine-specific types.
/// </summary>
public sealed class SceneGraph
{
    private readonly List<SceneNode> _roots = new();

    /// <summary>
    /// Top-level root nodes in the scene.
    /// </summary>
    public IReadOnlyList<SceneNode> Roots => _roots;

    /// <summary>
    /// Adds a new root-level node. Throws if a root with the same name already exists.
    /// </summary>
    public SceneNode AddRoot(string name)
    {
        if (_roots.Exists(r => r.Name == name))
            throw new ArgumentException($"A root node named '{name}' already exists.");

        var node = new SceneNode(name);
        _roots.Add(node);
        return node;
    }

    /// <summary>
    /// Removes a root node by name. Returns true if found and removed.
    /// Also removes all descendants.
    /// </summary>
    public bool RemoveRoot(string name)
    {
        var index = _roots.FindIndex(r => r.Name == name);
        if (index < 0) return false;

        _roots.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Finds a node by name across the entire scene using depth-first search.
    /// Returns the first match found.
    /// </summary>
    public SceneNode? Find(string name)
    {
        foreach (var root in _roots)
        {
            var found = root.Find(name);
            if (found is not null) return found;
        }

        return null;
    }

    /// <summary>
    /// Removes all nodes from the scene.
    /// </summary>
    public void Clear()
    {
        _roots.Clear();
    }
}
