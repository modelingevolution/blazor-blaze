using System.Diagnostics;

namespace BlazorBlaze.VectorGraphics;

public class FpsWatch
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public TimeSpan MeasurePeriod = TimeSpan.FromSeconds(5);
    private ulong _i = 0;
    private double _lastFps;
    public double Value => _lastFps;
    public static explicit operator double(FpsWatch watch)
    {
        return watch._lastFps;
    }
    public static FpsWatch operator ++(FpsWatch watch)
    {
        watch._i++;
        if (watch._sw.Elapsed <= watch.MeasurePeriod) return watch;

        watch._lastFps = watch._i * 1000.0 / watch._sw.Elapsed.TotalMilliseconds;
        watch._sw.Restart();
        watch._i = 0;
        return watch;
    }

    public override string ToString() => Value.ToString();

    public void Restart()
    {
        _sw.Restart();
    }
}
