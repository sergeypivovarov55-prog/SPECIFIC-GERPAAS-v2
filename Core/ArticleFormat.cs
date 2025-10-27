using System;
using System.Globalization;

namespace SpecificGerpaas.Core
{
    // Единый центр форматирования артикула GERPAAS.
    // Все методы статические, безопасные и с логированием.
    public static class ArticleFormat
    {
        private static readonly CultureInfo UaCulture = new CultureInfo("uk-UA");

        // ---------------------- Металл ----------------------
        public static string Metal(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                Log.Warn("[FORMAT] Порожнє покриття – застосовано 'PG'");
                return "PG";
            }

            string s = source.Trim().ToLowerInvariant();

            // Жёсткие правила по проекту
            if (s.Contains("ендз")) return "PG";        // Сендзимір
            if (s.Contains("занур")) return "HDG";      // Метод занурення

            Log.Warn($"[FORMAT] Невідоме покриття '{source}' – застосовано 'PG' за замовчуванням");
            return "PG";
        }

        // ---------------------- Толщина ----------------------
        public static string Thickness(double mm)
        {
            if (mm <= 0)
            {
                Log.Warn($"[FORMAT] Некоректна товщина '{mm}' – встановлено 1,0 мм");
                mm = 1.0;
            }

            // формат X,X
            return mm.ToString("0.0", UaCulture);
        }

        // ---------------------- Ширина мм → см ----------------------
        public static int WidthToCm(int mm)
        {
            if (mm <= 0)
            {
                Log.Warn($"[FORMAT] Некоректна ширина '{mm}' мм – встановлено 1 см");
                return 1;
            }
            return mm / 10;
        }

        // ---------------------- Висота → частина 'A{H}' ----------------------
        public static string HeightA(int heightMm)
        {
            if (heightMm <= 0)
            {
                Log.Warn($"[FORMAT] Некоректна висота '{heightMm}' мм – A100 за замовчуванням");
                heightMm = 100;
            }
            return $"A{heightMm}";
        }

        // ---------------------- Безпечне читання цілого ----------------------
        public static int ParseInt(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                Log.Error("[FORMAT] ParseInt: порожнє значення");
                throw new ArgumentException("ParseInt: Empty value");
            }

            raw = raw.ToLowerInvariant()
                     .Replace("мм", "")
                     .Replace(" ", "")
                     .Replace(",", ".")
                     .Trim();

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                return iv;

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
                return (int)Math.Round(dv);

            Log.Error($"[FORMAT] ParseInt: неможливо перетворити '{raw}' у число");
            throw new FormatException($"Invalid integer: '{raw}'");
        }

        // ---------------------- Безпечне читання double ----------------------
        public static double ParseDouble(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                Log.Error("[FORMAT] ParseDouble: порожнє значення");
                throw new ArgumentException("ParseDouble: Empty value");
            }

            raw = raw.ToLowerInvariant()
                     .Replace("мм", "")
                     .Replace(" ", "")
                     .Replace(",", ".")
                     .Trim();

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
                return dv;

            Log.Error($"[FORMAT] ParseDouble: неможливо перетворити '{raw}' у число");
            throw new FormatException($"Invalid double: '{raw}'");
        }
    }
}
