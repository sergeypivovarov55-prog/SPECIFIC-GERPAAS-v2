// -----------------------------------------------------------------------------
// SPECIFIC-GERPAAS : FamilyMapLoader.cs
// Формат секции [FamilyMap] в GERP_param_map.ini:
//
// [FamilyMap]
// # FamilyName = BaseArticle | GE_Категорія | GE_Додаткові
// S5_Sheet_Perforated tray = GE-KT2- | 1. Кабельні лотки | -
// -----------------------------------------------------------------------------
// Пробелы вокруг = и | игнорируются. Символ "-" означает пустое значение.
// Кодировка файла: UTF-8 без BOM.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SpecificGerpaas.Utils;

namespace SpecificGerpaas.Core
{
    public class FamilyMapRow
    {
        public string FamilyName { get; set; }
        public string BaseArticle { get; set; }
        public string Category { get; set; }
        public string Additional { get; set; }
    }

    public static class FamilyMapLoader
    {
        public static Dictionary<string, FamilyMapRow> Load()
        {
            var map = new Dictionary<string, FamilyMapRow>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string dataDir = Path.Combine(exeDir, "Data");
                string path = Path.Combine(dataDir, "GERP_param_map.ini");

                if (!File.Exists(path))
                {
                    Log.Error($"[FamilyMap] Не найден файл {path}");
                    return map;
                }

                bool inSection = false;
                int count = 0;

                foreach (string raw in File.ReadAllLines(path, System.Text.Encoding.UTF8))
                {
                    string line = raw.Trim();

                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    if (line.Equals("[FamilyMap]", StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        continue;
                    }

                    if (line.StartsWith("[") && !line.Equals("[FamilyMap]", StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = false;
                        continue;
                    }

                    if (!inSection || !line.Contains("="))
                        continue;

                    string[] keyVal = line.Split(new[] { '=' }, 2);
                    if (keyVal.Length < 2)
                        continue;

                    string family = keyVal[0].Trim();
                    string[] parts = keyVal[1].Split('|');

                    string baseArt = parts.Length > 0 ? parts[0].Trim() : "";
                    string cat = parts.Length > 1 ? parts[1].Trim() : "";
                    string add = parts.Length > 2 ? parts[2].Trim() : "";

                    if (baseArt == "-") baseArt = "";
                    if (cat == "-") cat = "";
                    if (add == "-") add = "";

                    var row = new FamilyMapRow
                    {
                        FamilyName = family,
                        BaseArticle = baseArt,
                        Category = cat,
                        Additional = add
                    };

                    map[family] = row;
                    count++;
                }

                Log.Info($"[FamilyMap] Загружено записей: {count}");
            }
            catch (Exception ex)
            {
                Log.Error("[FamilyMap] Ошибка загрузки: " + ex.Message);
            }

            return map;
        }
    }
}
