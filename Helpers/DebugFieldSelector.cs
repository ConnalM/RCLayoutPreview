using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace RCLayoutPreview.Helpers  // or whatever namespace you’re using
{
    public class DebugFieldSelector : Adorner
    {
        public DebugFieldSelector(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var rect = new Rect(AdornedElement.RenderSize);
            var pen = new Pen(Brushes.DeepSkyBlue, 1);
            drawingContext.DrawRectangle(null, pen, rect);
        }
    }
}