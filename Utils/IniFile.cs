using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpecificGerpaas.Utils
{
    public class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>();

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            if (!File.Exists(path))
                return ini;

            string currentSection = ""; // пустая секция = глобальные ключи
            string[] lines = TryReadAllLinesUtf8WithFallback(path);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int i = line.IndexOf('=');
                if (i < 0) continue;

                string key = line.Substring(0, i).Trim();
                string value = line.Substring(i + 1).Trim();

                if (!ini._sections.ContainsKey(currentSection))
                    ini._sections[currentSection] = new Dictionary<string, string>();

                ini._sections[currentSection][key] = value;
            }

            return ini;
        }

        /// <summary>
        /// Попытка прочитать файл как UTF-8, иначе fallback в Default (ANSI/1251)
        /// </summary>
        private static string[] TryReadAllLinesUtf8WithFallback(string path)
        {
            try
            {
                return File.ReadAllLines(path, Encoding.UTF8);
            }
            catch
            {
                // если файл не в UTF-8, читаем системной ANSI
                return File.ReadAllLines(path, Encoding.Default);
            }
        }

        /// <summary>
        /// Получить значение по ключу в секции (пустая строка = глобальная).
        /// </summary>
        public string Read(string key, string section = "")
        {
            if (_sections.TryGetValue(section, out var dict))
            {
                if (dict.TryGetValue(key, out var val))
                    return val;
            }
            return null;
        }
    }
}
