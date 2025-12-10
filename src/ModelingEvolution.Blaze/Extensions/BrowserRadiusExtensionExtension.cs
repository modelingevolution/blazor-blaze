namespace ModelingEvolution.Blaze
{
    class BrowserRadiusExtensionExtension() : BrowserEngineExtension<CircleControl, BrowserRadius>(OnChange)
    {
        private static void OnChange(CircleControl i, ScalingArgs e)
        {
            var radius = i.BrowserRadius();
            i.Radius = radius.Radius / e.Computed;
        }
    }
}
