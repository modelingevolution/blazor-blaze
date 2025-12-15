using BlazorBlaze.Server;
using BlazorBlaze.VectorGraphics;
using MudBlazor.Services;
using MudBlazorSample.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseWebSockets();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();

// VectorGraphics WebSocket test endpoint - bouncing ball animation
app.MapVectorGraphicsEndpoint("/ws/test-ball", PatternType.BouncingBall);

// MJPEG streaming endpoint for looping playback
var videosPath = Path.Combine(app.Environment.WebRootPath, "videos");
app.MapMjpegEndpoint("/mjpeg/{filename}", videosPath);
app.MapMjpegFrameEndpoint("/mjpeg-frame/{filename}/{frame:int}", videosPath);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MudBlazorSample.Client._Imports).Assembly);

app.Run();
