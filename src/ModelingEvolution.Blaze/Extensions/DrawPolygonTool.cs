using System.Diagnostics;
using ModelingEvolution.Drawing;

using SkiaSharp;

namespace ModelingEvolution.Blaze
{
    public class DrawPolygonTool : IEngineExtension
    {
        private BlazeEngine _engine;
        private List<LineControl> _path = new();
        private LineControl? _lastLine;
        private SKPoint _pathEnd;
        public event EventHandler<Polygon<float>> PolygonCreated;
        public void Bind(BlazeEngine engine)
        {
            this._engine = engine;
            engine.Scene.Root.OnMouseDown += OnClick;
            engine.Scene.Root.OnMouseMove += OnMove;
            engine.Scene.Root.OnDbClick += OnDbClick;
        }

        public void Clear()
        {
            foreach (var i in _path)
                this._engine.Scene.RemoveControl(i);
            _path.Clear();
            _lastLine = null;
        }
        private void OnDbClick(object? sender, MouseEventArgs e)
        {
            if(e.CtrlKey) return;
            if (_lastLine == null) return;
            
            _path.Add(_lastLine);
            if (_path.Count > 2)
            {
                _path.Add(CreateLine(_lastLine.EndPoint, _path[0].StartPoint));
                var polygon = new Polygon<float>(_path.Select(x=>x.StartPoint.AsPoint()).ToList());
                // Making sure we don't have wrong'y clicked points.
                polygon = polygon.Simplify(2);
                PolygonCreated?.Invoke(this, polygon);
            }
            else
                // it's not a path.
                Clear();

            _lastLine = null;

        }

        private void OnMove(object? sender, MouseEventArgs e)
        {
            if (sender != _engine.Scene.Root) return;
            if (e.CtrlKey) return;
            if (_lastLine != null)
            {
                _lastLine.EndPoint = e.WorldAbsoluteLocation;
            }
        }

        private LineControl CreateLine(SKPoint start, SKPoint end)
        {
            var tmp  = new LineControl(start,end);
            tmp.BrowserStrokeWidth().StrokeWidth = 4;
            tmp.Stroke = SKColors.LawnGreen;
            tmp.IsHitEnabled = false;
            _engine.Scene.AddControl(tmp);
            return tmp;
        }
        private void OnClick(object? sender, MouseEventArgs e)
        {
            if (sender != _engine.Scene.Root) return;
            if (e.CtrlKey) return;
            
            bool first = _lastLine == null;
            if (!first)
            {
                if (_lastLine.StartPoint == _lastLine.EndPoint)
                    return;
                // finish last part.
                _lastLine.EndPoint = e.WorldAbsoluteLocation;
                _path.Add(_lastLine);
            }
            _pathEnd = e.WorldAbsoluteLocation;
            _lastLine = CreateLine(_pathEnd, e.WorldAbsoluteLocation);
            _engine.Scene.AddControl(_lastLine);

        }

        public void Unbind(BlazeEngine engine)
        {
            engine.Scene.Root.OnMouseDown -= OnClick;
            engine.Scene.Root.OnMouseMove -= OnMove;
            engine.Scene.Root.OnDbClick -= OnDbClick;
        }
    }
}
