using System.IO;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Resolves asset file paths. Checks organised subfolders first
    /// (Assets\Images, Assets\Sounds), then falls back to the flat
    /// layout next to the executable for backward compatibility.
    /// </summary>
    internal static class AssetPath
    {
        private static string _root;
        private static string Root
        {
            get
            {
                if (_root == null)
                    _root = Application.StartupPath;
                return _root;
            }
        }

        /// <summary>Resolve an image file (png, etc.).</summary>
        public static string Image(string fileName)
        {
            string sub = Path.Combine(Root, "Assets", "Images", fileName);
            if (File.Exists(sub)) return sub;
            return Path.Combine(Root, fileName);
        }

        /// <summary>Resolve a sound file (mp3, wav, etc.).</summary>
        public static string Sound(string fileName)
        {
            string sub = Path.Combine(Root, "Assets", "Sounds", fileName);
            if (File.Exists(sub)) return sub;
            return Path.Combine(Root, fileName);
        }
    }
}
