using Microsoft.AspNetCore.Components;

namespace BlazorBlaze.Scene3D;

/// <summary>
/// Interface that rendering adapters implement to translate SceneGraph to a rendered surface.
/// Scene3D ships with no implementation -- adapters are separate packages.
/// </summary>
public interface ISceneRenderer : IAsyncDisposable
{
    /// <summary>
    /// Called once when the component is initialized. The adapter creates its rendering surface.
    /// </summary>
    /// <param name="hostElement">The DOM element that will host the rendered output.</param>
    Task InitializeAsync(ElementReference hostElement);

    /// <summary>
    /// Renders the scene graph with the given camera.
    /// Called each time the scene or camera changes.
    /// Sync adapters can return <see cref="Task.CompletedTask"/>.
    /// </summary>
    Task RenderAsync(SceneGraph scene, Camera3D camera);

    /// <summary>
    /// Async callback invoked when a scene node is clicked in the rendered view.
    /// Adapters populate NodeClickedEventArgs with as much detail as they support
    /// (WorldPosition may be null if the adapter lacks raycasting).
    /// </summary>
    Func<NodeClickedEventArgs, Task>? OnNodeClicked { get; set; }
}
