namespace ModelingEvolution.Blaze;

public interface IControlExtension
{
    void Bind(Control control, BlazeEngine engine);
    void Unbind(Control control, BlazeEngine engine);
}