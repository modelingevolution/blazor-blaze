namespace BlazorBlaze;

interface IEngineExtension
{
    void Bind(BlazeEngine engine);
    void Unbind(BlazeEngine engine);
}