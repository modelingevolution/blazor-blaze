using SkiaSharp;

namespace ModelingEvolution.Blaze
{
    public class BitmapControl(SKBitmap bitmap) : Control
    {

        public static BitmapControl FromStream(Stream stream)
        {
            using var ms = new SKManagedStream(stream, false);
            var bitmap = SKBitmap.Decode(ms);
            return new BitmapControl(bitmap);
        }
        public override void Render(SKCanvas canvas, SKRect viewport)
        {
            canvas.DrawBitmap(bitmap, AbsoluteOffset);
        }

        public override void RenderForHitMap(SKCanvas canvas, SKPaint paint)
        {
            //var off = AbsoluteOffset;
            //canvas.DrawRect(off.X, off.Y, bitmap.Width, bitmap.Height, paint);
        }
    }
}
