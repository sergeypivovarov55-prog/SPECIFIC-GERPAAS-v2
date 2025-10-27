using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpecificGerpaas.Utils;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.UI;

namespace SpecificGerpaas.Core
{
    /// <summary>
    /// Управление значениями комбо-боксов DKC через INI-файл GERP_param_map.ini.
    /// </summary>
    public static class DkcComboManager
    {
        private static string _iniPath;
        private static IniFile _ini;
        public static ComboBox ThicknessCombo { get; set; }
        public static ComboBox CoatingCombo { get; set; }
        public static void Init()
        {
            try
            {
                var dllDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );

                _iniPath = Path.Combine(dllDir, "Data", "GERP_param_map.ini");

                _ini = IniFile.Load(_iniPath);

                if (!File.Exists(_iniPath))
                {
                    Log.Warn("[DkcComboManager] Файл настроек не найден: " + _iniPath);
                    return;
                }

                //// логировать только при первом вызове
                //if (!_iniLoaded)
                //{
                //    Log.Info($"[DkcComboManager] Используется файл настроек: {_iniPath}");
                //    _iniLoaded = true;
                //}
            }
            catch (Exception ex)
            {
                Log.Error("[DkcComboManager] Ошибка инициализации", ex);
            }
        }

        public static List<string> GetThkList()
        {
            string s = _ini != null ? _ini.Read("ThkSet", "Combobocks_Setting") : null;
            if (string.IsNullOrWhiteSpace(s)) s = "1,0 мм | 1,2 мм | 1,5 мм | 2,0 мм";

            var list = s.Split('|')
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

            //Log.Info("[DkcComboManager] Толщины: " + string.Join(", ", list.ToArray()));
            return list;
        }

        public static string GetThkCurrent()
        {
            string val = _ini != null ? _ini.Read("ThkCur", "Combobocks_Setting") : "";
            if (val == null) val = "";
            val = val.Trim();
            //Log.Info("[DkcComboManager] Текущая толщина: " + val);
            return val;
        }

        public static List<string> GetCoatList()
        {
            string s = _ini != null ? _ini.Read("CoatSet", "Combobocks_Setting") : null;
            if (string.IsNullOrWhiteSpace(s)) s = "Сендзимир | Занурення";

            var list = s.Split('|')
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

            //Log.Info("[DkcComboManager] Покриття: " + string.Join(", ", list.ToArray()));
            return list;
        }

        public static string GetCoatCurrent()
        {
            string val = _ini != null ? _ini.Read("CoatCur", "Combobocks_Setting") : "";
            if (val == null) val = "";
            val = val.Trim();
            //Log.Info("[DkcComboManager] Текущее покрытие: " + val);
            return val;
        }

        public static void SaveThkCurrent(string value)
        {
            SaveValue("Combobocks_Setting", "ThkCur", value);
        }

        public static void SaveCoatCurrent(string value)
        {
            SaveValue("Combobocks_Setting", "CoatCur", value);
        }

        /// <summary>
        /// Простая запись значения (перезаписывает весь файл).
        /// </summary>
        private static void SaveValue(string section, string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(_iniPath) || !File.Exists(_iniPath)) return;

                var lines = File.ReadAllLines(_iniPath, Encoding.UTF8).ToList();

                // найдём границы нужной секции
                int secStart = -1, secEnd = lines.Count;
                for (int i = 0; i < lines.Count; i++)
                {
                    var t = lines[i].Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        var secName = t.Substring(1, t.Length - 2).Trim();
                        if (secStart >= 0) { secEnd = i; break; }
                        if (secName.Equals(section, System.StringComparison.OrdinalIgnoreCase))
                            secStart = i;
                    }
                }

                var keyRegex = new Regex(@"^\s*" + Regex.Escape(key) + @"\s*=", RegexOptions.IgnoreCase);
                bool replaced = false;
                var toRemove = new List<int>();

                if (secStart >= 0)
                {
                    // проходим строки только внутри секции
                    for (int i = secStart + 1; i < secEnd; i++)
                    {
                        if (keyRegex.IsMatch(lines[i]))
                        {
                            if (!replaced)
                            {
                                lines[i] = key + " = " + value; // заменить первую найденную
                                replaced = true;
                            }
                            else
                            {
                                toRemove.Add(i); // удалить возможные дубликаты ключа
                            }
                        }
                    }

                    // удаляем дубликаты снизу вверх
                    for (int k = toRemove.Count - 1; k >= 0; k--) lines.RemoveAt(toRemove[k]);

                    if (!replaced)
                    {
                        // ключа не было — вставляем сразу после заголовка секции
                        lines.Insert(secStart + 1, key + " = " + value);
                    }
                }
                else
                {
                    // секции не было вовсе — создаём внизу
                    lines.Add("");
                    lines.Add("[" + section + "]");
                    lines.Add(key + " = " + value);
                }

                File.WriteAllLines(_iniPath, lines.ToArray(), Encoding.UTF8);

                // обновляем кэш в памяти (чтобы Get* возвращали новое значение без перезапуска)
                _ini = IniFile.Load(_iniPath);
                Log.Info("[DkcComboManager] Сохранено " + key + " = " + value);
            }
            catch (System.Exception ex)
            {
                Log.Error("[DkcComboManager] Ошибка записи " + key, ex);
            }

        }

        public static int GetTrayPieceLength()
        {
            // читаем длину секции лотка из INI (ключ TrayWidth = 3000)
            if (_ini == null)
            {
                Log.Warn("[DkcComboManager] GetTrayPieceLength: ini не инициализирован, используем 3000 мм");
                return 3000;
            }

            string raw = _ini.Read("TrayWidth", "Combobocks_Setting");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // оставим только цифры
                var sb = new StringBuilder();
                for (int i = 0; i < raw.Length; i++)
                {
                    char ch = raw[i];
                    if (ch >= '0' && ch <= '9') sb.Append(ch);
                }

                int val;
                if (int.TryParse(sb.ToString(), out val) && val > 0)
                {
                    //Log.Info("[DkcComboManager] TrayWidth (ини) = " + val + " мм");
                    return val;
                }
            }

            Log.Info("[DkcComboManager] TrayWidth не задан или некорректен — используем 3000 мм");
            return 3000;
        }

        public static string GetSelectedThickness()
        {
            // если комбо ещё не назначен — fallback в ini
            if (ThicknessCombo == null)
                return GetThkCurrent();

            var selected = ThicknessCombo.Current?.ItemText;
            if (string.IsNullOrWhiteSpace(selected))
                return GetThkCurrent();

            return selected.Trim();
        }

        public static string GetSelectedCoating()
        {
            if (CoatingCombo == null)
                return GetCoatCurrent();

            var selected = CoatingCombo.Current?.ItemText;
            if (string.IsNullOrWhiteSpace(selected))
                return GetCoatCurrent();

            return selected.Trim();
        }

    }
}
