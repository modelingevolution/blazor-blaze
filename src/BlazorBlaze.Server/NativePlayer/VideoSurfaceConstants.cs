namespace BlazorBlaze.Server.NativePlayer;

internal static class VideoSurfaceConstants
{
    internal const string ModulePath = "./_content/BlazorBlaze.Server/video-surface.js";
}

public readonly record struct PlayerRect(int X, int Y, int Width, int Height);
