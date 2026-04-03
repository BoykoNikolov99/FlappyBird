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
        private static readonly string Root = Application.StartupPath;
        private static readonly string ImagesDir = Path.Combine(Root, "Assets", "Images");
        private static readonly string SoundsDir = Path.Combine(Root, "Assets", "Sounds");

        /// <summary>Resolve an image file (png, etc.).</summary>
        public static string Image(string fileName)
        {
            string sub = Path.Combine(ImagesDir, fileName);
            if (File.Exists(sub)) return sub;
            return Path.Combine(Root, fileName);
        }

        /// <summary>Resolve a sound file (mp3, wav, etc.).</summary>
        public static string Sound(string fileName)
        {
            string sub = Path.Combine(SoundsDir, fileName);
            if (File.Exists(sub)) return sub;
            return Path.Combine(Root, fileName);
        }
    }
}
