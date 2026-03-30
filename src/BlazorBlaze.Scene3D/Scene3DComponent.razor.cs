using Microsoft.AspNetCore.Components;

namespace BlazorBlaze.Scene3D;

/// <summary>
/// Blazor component shell that takes a SceneGraph, Camera3D, and an ISceneRenderer.
/// Delegates all rendering to the adapter. Supports node click events.
/// </summary>
public partial class Scene3DComponent : ComponentBase, IAsyncDisposable
{
    private ElementReference _hostElement;
    private bool _initialized;

    /// <summary>
    /// The scene graph to render.
    /// </summary>
    [Parameter]
    public SceneGraph? Scene { get; set; }

    /// <summary>
    /// The camera used for viewing the scene.
    /// </summary>
    [Parameter]
    public Camera3D? Camera { get; set; }

    /// <summary>
    /// The rendering adapter. Injected via DI or set as parameter.
    /// </summary>
    [Parameter]
    public ISceneRenderer? Renderer { get; set; }

    /// <summary>
    /// Callback when a node is clicked in the rendered view.
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnNodeClicked { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Renderer is not null)
        {
            await Renderer.InitializeAsync(_hostElement);
            Renderer.NodeClicked += HandleNodeClicked;
            _initialized = true;
            Refresh();
        }
    }

    /// <summary>
    /// Forces a re-render of the scene. Call after modifying SceneGraph or Camera3D.
    /// </summary>
    public void Refresh()
    {
        if (!_initialized || Renderer is null || Scene is null || Camera is null) return;
        Renderer.Render(Scene, Camera);
    }

    private void HandleNodeClicked(string? nodeName)
    {
        if (OnNodeClicked.HasDelegate)
        {
            InvokeAsync(() => OnNodeClicked.InvokeAsync(nodeName));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Renderer is not null)
        {
            Renderer.NodeClicked -= HandleNodeClicked;
            if (_initialized)
            {
                await Renderer.DisposeAsync();
            }
        }
    }
}
