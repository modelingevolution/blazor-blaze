using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelingEvolution;
using SkiaSharp;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics;

public class StreamingCanvasEngine
{
    private readonly ProtoStreamClient _client;
    private readonly SkiaCanvas _canvas;
    private CancellationTokenSource? _cts;
    private HashSet<int>? _filteredLayers;
    public StreamingCanvasEngine(ILoggerFactory factory)
    {
        var r = MessageRegisterBuilder.Create()
            .With(1, typeof(Draw<Text>))
            .With(2, typeof(Draw<Polygon>)).Build();
        
        _client = new ProtoStreamClient(new Serializer(r), factory.CreateLogger<ProtoStreamClient>());
        _canvas = new SkiaCanvas();
    }
    public async Task ConnectAsync(Uri uri)
    {
        if (IsRunning) throw new InvalidOperationException("Engine is already running");
        Uri = uri;
        await _client.ConnectAsync(uri);
        _cts = new CancellationTokenSource();
        _ = Task.Factory.StartNew(OnStartStreaming, TaskCreationOptions.LongRunning);
    }
    public Uri Uri { get; private set; }
    public ulong Frame { get; private set; }
    public float Lps { get; private set; }
    public bool IsRunning { get; private set; }
    public string? Error { get; private set; }
    public int[]? FilteredLayers
    {
        get => _filteredLayers?.ToArray();
        set
        {
            if (value == null)
            {
                _filteredLayers = null;
                return;
            }

            var filteredLayers = new HashSet<int>(value.Distinct());
            _filteredLayers = filteredLayers;
            
        } 
    }
    
    private async Task OnStartStreaming()
    {
        try
        {
            IsRunning = true;
            Error = null;
            FpsWatch sw = new();

            await foreach (var i in _client.Read(_cts!.Token))
            {
                var ls = _filteredLayers;
                if (ls != null && !ls.Contains(i.LayerId)) continue;

                _canvas.Begin(i.Number, i.LayerId);

                Frame = i.Number;
                Lps = (float)sw++.Value;
                if(i.IsInitialized)
                {
                    for (int j = 0; j < i.Count; j++)
                    {
                        if (i[j] is IRenderOp renderOp)
                            _canvas.Add(renderOp, i.LayerId);
                    }
                }
                else
                {
                    // Frame is not initialized: skipping render
                }

                _canvas.End(i.LayerId);
            }

        }
        catch (Exception ex)
        {
            Error = ex.Message;
            // Log to proper logging framework if needed
        }
        IsRunning = false;
        _cts = null;
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            await _client.DisconnectAsync();
        }
    }
    public void Render(SKCanvas canvas)
    {
        canvas.Clear();
        _canvas.Render(canvas);
    }
}

public class PeriodicConsoleWriter(TimeSpan period)
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly TimeSpan _period = period;

    public void WriteLine(string text)
    {
        if (_sw.Elapsed <= _period) return;
        // Periodic logging disabled for performance
        _sw.Restart();
    }
}
public class TransferWatch
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public TimeSpan MeasurePeriod = TimeSpan.FromSeconds(5);
    private Bytes _i = 0;
    private Bytes _lastBps;
    public Bytes Value => _lastBps;
    public static explicit operator Bytes(TransferWatch watch)
    {
        return watch._lastBps;
    }
    public static TransferWatch operator +(TransferWatch watch, Bytes bytes)
    {
        watch._i += bytes;
        if (watch._sw.Elapsed <= watch.MeasurePeriod) return watch;

        watch._lastBps = (long)(watch._i * 1000 / watch._sw.Elapsed.TotalMilliseconds);
        watch._sw.Restart();
        watch._i = 0;
        return watch;
    }

    public override string ToString() => _lastBps.ToString();
}