using System.Windows;

namespace RCLayoutPreview.Helpers
{
    public static class WindowPlacementHelper
    {
        public static void SaveWindowPlacement(Window window, string prefix)
        {
            var settings = Properties.Settings.Default;
            settings[$"{prefix}Left"] = window.Left;
            settings[$"{prefix}Top"] = window.Top;
            settings[$"{prefix}Width"] = window.Width;
            settings[$"{prefix}Height"] = window.Height;
            settings.Save();
        }

        public static bool RestoreWindowPlacement(Window window, string prefix)
        {
            var settings = Properties.Settings.Default;
            double left = (double)settings[$"{prefix}Left"];
            double top = (double)settings[$"{prefix}Top"];
            double width = (double)settings[$"{prefix}Width"];
            double height = (double)settings[$"{prefix}Height"];
            if (left >= 0 && top >= 0 && width > 0 && height > 0)
            {
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;
                return true;
            }
            return false;
        }
    }
}
