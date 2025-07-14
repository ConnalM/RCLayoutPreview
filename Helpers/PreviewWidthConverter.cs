using System;
using System.Globalization;
using System.Windows.Data;

namespace RCLayoutPreview
{
    public class PreviewWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double canvasWidth)
            {
                // Calculate preview width (canvas width - left offset - some margin)
                double previewWidth = Math.Max(200, canvasWidth - 610 - 10);
                return previewWidth;
            }
            return 400; // Default width if calculation fails
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}