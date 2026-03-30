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
    /// </summary>
    void Render(SceneGraph scene, Camera3D camera);

    /// <summary>
    /// Occurs when a scene node is clicked in the rendered view.
    /// The string parameter is the clicked node's name, or null if no node was hit.
    /// </summary>
    event Action<string?>? NodeClicked;
}
