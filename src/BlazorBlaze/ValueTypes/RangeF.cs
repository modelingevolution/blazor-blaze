using System.ComponentModel.DataAnnotations;

namespace BlazorBlaze;

public readonly record struct RangeF
{
    public float Min { get; }
    public float Max { get; }

    public RangeF(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public bool Contains(float value) => value >= Min && value <= Max;
    public float Clamp(float value) => Math.Clamp(value, Min, Max);
    public override string ToString() => $"[{Min}, {Max}]";
}