using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    internal static class IconHelper
    {
        private static Bitmap _bitmap;
        private static Icon _appIcon;

        public static Icon AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    string path = AssetPath.Image("flappy-bird.png");
                    if (File.Exists(path))
                    {
                        try
                        {
                            _bitmap = new Bitmap(path);
                            _appIcon = Icon.FromHandle(_bitmap.GetHicon());
                        }
                        catch { }
                    }
                }
                return _appIcon;
            }
        }

        public static void SetFormIcon(Form form)
        {
            var icon = AppIcon;
            if (icon != null)
                form.Icon = icon;
        }
    }
}
