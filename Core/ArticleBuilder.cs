// -----------------------------------------------------------------------------
// ArticleBuilder.cs  (версия 3.0)
// -----------------------------------------------------------------------------

using System;

namespace SpecificGerpaas.Core
{
    public static class ArticleBuilder
    {
        public static string BuildArticle(
            string baseArticle, int w, int h, double thicknessMm, string metal, double? angle)
        {
            if (string.IsNullOrWhiteSpace(baseArticle))
                return "";

            string metalCode = ToMetalCode(metal);           // --- Вызов помощника для кода покрытия
            string widthCm = ToCm(w).ToString();             // --- Вызов помощника для преобразования размера ширины
            string heightPart = "A" + h.ToString();          // --- Вызов помощника для преобразования размера высоты
            string thickPart = FormatThick(thicknessMm);     // --- Вызов помощника для преобразования размера толщины

            // --- Определяем тип по BaseArticle ---
            string lowBase = baseArticle.ToLowerInvariant();
            bool isCover = lowBase.Contains("ktk") || lowBase.Contains("bk") || lowBase.Contains("k1") || lowBase.EndsWith("k-");
            bool isFitting = lowBase.Contains("d") || lowBase.Contains("ib") || lowBase.Contains("ob");
            bool isTray = !(isCover || isFitting);

            // --- Формируем тело артикула ---
            string result;
            if (baseArticle.EndsWith("-", StringComparison.Ordinal))
                result = baseArticle;
            else
                result = baseArticle + "-";

            // --- Крышки ---
            if (isCover)
            {
                // GE-KTK1-20-1,2-HDG       ← крышка на прямой участок
                // GE-DK-20-1,2-HDG         ← крышка на 90°
                // GE-DK45-20-1,2-HDG       ← крышка на 45°
                // GE-GE-OBK90-20-1,2-HDG   ← крышка на 90°
                // GE-GE-OBK45-20-1,2-HDG   ← крышка на 45°

                string anglePart = "";

                if (baseArticle.StartsWith("GE-DK", StringComparison.OrdinalIgnoreCase) && angle.HasValue)
                {
                    int angInt = (int)Math.Round(angle.Value);
                    if (angInt == 45)
                    {
                        // убираем дефис, если baseArticle заканчивается на "-"
                        baseArticle = baseArticle.TrimEnd('-');
                        anglePart = "45";
                    }
                }

                // --- Внешняя крышка GE-OBK ---
                if (baseArticle.StartsWith("GE-OBK", StringComparison.OrdinalIgnoreCase) && angle.HasValue)
                {
                    int angInt = (int)Math.Round(angle.Value);
                    if (angInt == 45 || angInt == 90)
                    {
                        baseArticle = baseArticle.TrimEnd('-');
                        anglePart = angInt.ToString();
                    }
                }

                // --- Внутренняя крышка GE-IBK ---
                if (baseArticle.StartsWith("GE-IBK", StringComparison.OrdinalIgnoreCase) && angle.HasValue)
                {
                    int angInt = (int)Math.Round(angle.Value);
                    if (angInt == 45 || angInt == 90)
                    {
                        baseArticle = baseArticle.TrimEnd('-');
                        anglePart = angInt.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(anglePart))
                    result = $"{baseArticle}{anglePart}-{widthCm}-{thickPart}-{metalCode}";
                else
                    result += widthCm + "-" + thickPart + "-" + metalCode;
            }

            else if (isFitting)
            {
                // --- фитинги (горизонтальные и вертикальные углы) ---
                string anglePart = "";

                if (angle.HasValue)
                {
                    int angInt = (int)Math.Round(angle.Value);

                    // горизонтальные углы (все)
                    if (baseArticle.Contains("-D"))
                    {
                        anglePart = angInt.ToString();
                    }
                    // внутренние вертикальные (IB)
                    else if (baseArticle.Contains("-IB"))
                    {
                        baseArticle = baseArticle.TrimEnd('-');
                        if (angInt == 45 || angInt == 90)
                            anglePart = angInt.ToString();
                    }
                    // внешние вертикальные (OB)
                    else if (baseArticle.Contains("-OB"))
                    {
                        baseArticle = baseArticle.TrimEnd('-');
                        if (angInt == 45 || angInt == 90)
                            anglePart = angInt.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(anglePart))
                    result = $"{baseArticle}{anglePart}-{widthCm}-{heightPart}-{thickPart}-{metalCode}";
                else
                    result = $"{baseArticle}-{widthCm}-{heightPart}-{thickPart}-{metalCode}";
            }

            else
            {
                // лотки (по умолчанию)
                result += widthCm + "-" + heightPart + "-" + thickPart + "-" + metalCode;
            }

            // --- Очистка от двойных дефисов ---
            while (result.Contains("--"))
                result = result.Replace("--", "-");
            result = result.Trim('-');

            // ---------- DEFAULT / FALLBACK ----------

            return result;
        }

        // ---------------------- ПОМОЩНИКИ ----------------------
        // Единые помощники для формата артикула
        private static string ToMetalCode(string metal)
        {
            if (string.IsNullOrEmpty(metal)) return "PG";
            var low = metal.ToLowerInvariant();
            if (low.Contains("занур")) return "HDG";   // «Гаряче цинкування»
            if (low.Contains("сендз")) return "PG";    // «Сендзимір»
            if (low.Contains("алюм")) return "AL";
            return "PG";
        }

        private static string FormatThick(double mm)
        {
            // Всегда "X,X" с запятой
            return mm.ToString("0.0", new System.Globalization.CultureInfo("uk-UA"));
        }

        private static int ToCm(int mm) => mm / 10; // 200 → 20

    }
}
