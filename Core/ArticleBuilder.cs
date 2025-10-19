// -----------------------------------------------------------------------------
// ArticleBuilder.cs  (версия 3.0)
// -----------------------------------------------------------------------------
// Формирует артикулы для лотков, углов, крышек и т.д.
// Поддерживает правила:
//  - Углы: не добавляем угол в конец (он зашит в BaseArticle);
//  - Крышки: не добавляем высоту и угол;
//  - Лотки: полный формат.
// -----------------------------------------------------------------------------
using Autodesk.Revit.DB;
using SpecificGerpaas.Utils;
using System;
using System.Globalization;

namespace SpecificGerpaas.Core
{
    public static class ArticleBuilder
    {
        public static string BuildArticle(
            string baseArticle, int w, int h, double thicknessMm, string metal, double? angle)
        {
            if (string.IsNullOrWhiteSpace(baseArticle))
                return "";

            // --- Код покрытия ---
            string metalCode = "PG"; // default
            if (!string.IsNullOrEmpty(metal))
            {
                var low = metal.ToLowerInvariant();
                if (low.Contains("цинк"))
                    metalCode = "HDG";
                else if (low.Contains("сендз"))
                    metalCode = "PG";
                else if (low.Contains("алюм"))
                    metalCode = "AL";
            }

            // --- Преобразование размеров ---
            string widthCm = (w / 10).ToString(); // 200 → 20
            string heightPart = "A" + h.ToString(); // 100 → A100
            string thickPart = thicknessMm.ToString("0.0", new CultureInfo("uk-UA"));

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

            if (isCover)
            {
                // --- крышки ---
                // GE-KTK1-20-1,2-HDG   ← крышка на прямой участок
                // GE-DK-20-1,2-HDG     ← крышка на 90°
                // GE-DK45-20-1,2-HDG   ← крышка на 45°

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

            return result;
        }

        // Универсальный билдер угловых элементов
            public static string BuildBendArticle(Element e, string famName, double thicknessMm, string metal)
            {
                // --- Код покрытия ---
                string metalCode = "PG"; // default
                if (!string.IsNullOrEmpty(metal))
                {
                    var low = metal.ToLowerInvariant();
                    if (low.Contains("цинк"))
                        metalCode = "HDG";
                    else if (low.Contains("сендз"))
                        metalCode = "PG";
                    else if (low.Contains("алюм"))
                        metalCode = "AL";
                }

                // --- Получаем размеры и угол ---
                int w, h;
                double? angle;
                SizeHelper.GetWidthHeightAndAngle(e, out w, out h, out angle);

                string thickPart = thicknessMm.ToString("0.0", new CultureInfo("uk-UA"));
                thickPart = thickPart.Replace(",", ".");

                // ---------- 1. Horizontal Bend_CPO45-CPO90 ----------
                if (famName.Contains("Horizontal Bend_CPO45-CPO90"))
                {
                    string prefix = (angle ?? 90) >= 80 ? "GE-KT2-D90-" : "GE-KT2-D45-";
                    return $"{prefix}{w / 10}-A{h}-{thickPart}-{metalCode}";
                }

                // ---------- 2. Horizontal Bend_CPO0-45 ----------
                if (famName.Contains("Horizontal Bend_CPO0-45"))
                {
                    return $"GE-YDK-{h}-{thickPart}-{metalCode}";
                }

                // ---------- 3. Int Vertical Bend_CS45-CS90 ----------
                if (famName.Contains("Int Vertical Bend_CS45-CS90"))
                {
                    string prefix = (angle ?? 90) >= 80 ? "GE-KT2-IB90-" : "GE-KT2-IB45-";
                    return $"{prefix}{w / 10}-A{h}-{thickPart}-{metalCode}";
                }

                // ---------- 4. Int Vertical Bend_1-89 ----------
                if (famName.Contains("Int Vertical Bend_1-89"))
                {
                    return $"GE-KT2-IBF-{w / 10}-A{h}-{thickPart}-{metalCode}";
                }

                // ---------- 5. Ext Vertical Bend_CD45-CD90 ----------
                if (famName.Contains("Ext Vertical Bend_CD45-CD90"))
                {
                    string prefix = (angle ?? 90) >= 80 ? "GE-KT2-OB90-" : "GE-KT2-OB45-";
                    return $"{prefix}{w / 10}-A{h}-{thickPart}-{metalCode}";
                }

                // ---------- 6. Ext Vertical Bend_1-89 ----------
                if (famName.Contains("Ext Vertical Bend_1-89"))
                {
                    return $"GE-KT2-OBF-{w / 10}-A{h}-{thickPart}-{metalCode}";
                }

                return null;
            } 
    }
}
