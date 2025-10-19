using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace SpecificGerpaas.Utils
{
    public static class ImageLoader
    {
        /// <summary>
        /// Загружает PNG из Embedded Resource. Перебирает список имён до первого найденного.
        /// Примеры имён: "SpecificGerpaas.Icons.Gerpaas32.png", "Icons.Gerpaas32.png", "Gerpaas32.png"
        /// </summary>
        public static BitmapImage FromEmbedded(params string[] resourceNames)
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in resourceNames)
            {
                using (Stream s = asm.GetManifestResourceStream(name))
                {
                    if (s == null) continue;
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = s;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    return bi;
                }
            }
            return null;
        }
    }
}
