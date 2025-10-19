// Core/ScheduleExporter.cs
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SpecificGerpaas.Core
{
    public static class ScheduleExporter
    {
        public static string ExportScheduleToCsv(Document doc, string scheduleName)
        {
            var schedule = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(v => v.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase));

            if (schedule == null)
            {
                Log.Warn($"[Export] Спецификация '{scheduleName}' не найдена");
                return null;
            }

            var data = schedule.GetTableData();
            var head = data.GetSectionData(SectionType.Header);
            var body = data.GetSectionData(SectionType.Body);

            var asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var outDir = Path.Combine(asmDir, "Exports");
            Directory.CreateDirectory(outDir);
            var file = $"GE_Spec_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var path = Path.Combine(outDir, file);

            const string DELIM = ";";

            // Определяем числовые колонки по шапке (Кількість / Маса*)
            var numericCols = new HashSet<int>();
            if (head != null && head.NumberOfColumns > 0)
            {
                int headerRow = Math.Max(0, head.NumberOfRows - 1);
                for (int c = 0; c < head.NumberOfColumns; c++)
                {
                    var name = schedule.GetCellText(SectionType.Header, headerRow, c)?.Trim() ?? "";
                    if (name.Equals("Кількість", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Маса", StringComparison.OrdinalIgnoreCase))
                    {
                        numericCols.Add(c);
                    }
                }
            }

            using (var sw = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                // Header
                if (head != null && head.NumberOfColumns > 0)
                {
                    for (int r = 0; r < head.NumberOfRows; r++)
                    {
                        var cells = new List<string>(head.NumberOfColumns);
                        for (int c = 0; c < head.NumberOfColumns; c++)
                        {
                            var text = schedule.GetCellText(SectionType.Header, r, c);
                            cells.Add(CsvEscape(text));
                        }
                        sw.WriteLine(string.Join(DELIM, cells));
                    }
                }

                // Body
                if (body != null && body.NumberOfColumns > 0)
                {
                    for (int r = 0; r < body.NumberOfRows; r++)
                    {
                        var cells = new List<string>(body.NumberOfColumns);
                        for (int c = 0; c < body.NumberOfColumns; c++)
                        {
                            var text = schedule.GetCellText(SectionType.Body, r, c) ?? "";

                            if (numericCols.Contains(c) || IsNumericLike(text))
                            {
                                // Числа — без кавычек и с запятой
                                var num = CleanNumberForCsv(text);
                                cells.Add(num);
                            }
                            else
                            {
                                cells.Add(CsvEscape(text));
                            }
                        }
                        sw.WriteLine(string.Join(DELIM, cells));
                    }
                }
            }

            Log.Info($"[Export] Спецификация экспортирована: {path}");
            return path;
        }

        private static bool IsNumericLike(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            bool hasDigit = false;
            foreach (var ch in raw)
            {
                if (char.IsDigit(ch)) { hasDigit = true; continue; }
                if (ch == '-' || ch == ' ' || ch == '\u00A0' || ch == '.' || ch == ',') continue;
                if (char.IsLetter(ch)) return false;
            }
            return hasDigit;
        }

        private static string CleanNumberForCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw.Trim())
            {
                if (char.IsDigit(ch) || ch == '-' || ch == '.' || ch == ',')
                    sb.Append(ch);
            }
            var s = sb.ToString().Replace('.', ',');
            int ix = s.IndexOf(',');
            if (ix >= 0)
            {
                var head = s.Substring(0, ix + 1);
                var tail = s.Substring(ix + 1).Replace(",", "");
                s = head + tail;
            }
            s = s.Trim(',', ' ');
            return s;
        }

        private static string CsvEscape(string s)
        {
            if (s == null) return "\"\"";
            var t = s.Replace("\"", "\"\"");
            return "\"" + t + "\"";
        }
    }
}
