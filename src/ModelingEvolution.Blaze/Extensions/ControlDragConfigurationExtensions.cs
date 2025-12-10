namespace ModelingEvolution.Blaze;

public static class ControlDragConfigurationExtensions
{
    public static void EnableDrag(this Control control) => control.Extensions.Enable<ControlDragExtension>();

    public static void DisableDrag(this Control control) => control.Extensions.Disable<ControlDragExtension>();
}