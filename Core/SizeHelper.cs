// Utils/SizeHelper.cs
using Autodesk.Revit.DB;
using SpecificGerpaas.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SpecificGerpaas.Utils
{
    public static class SizeHelper
    {
        /// <summary>
        /// Извлекает первую пару ширины и высоты (мм) и угол (°) из параметров элемента.
        /// Поддерживаются оба формата "200 ммх100 мм" и "200/100", а также параметры DKC_ШиринаЛотка / DKC_ВысотаЛотка.
        /// </summary>
        public static void GetWidthHeightAndAngle(Element e, out int width, out int height, out double? angleDeg)
        {
            width = 0;
            height = 0;
            angleDeg = null;

            if (e == null)
                return;

            // 1 Попробуем получить напрямую из параметров DKC_ШиринаЛотка / DKC_ВысотаЛотка
            int w1 = TryGetSizeParam(e, "DKC_ШиринаЛотка");
            int h1 = TryGetSizeParam(e, "DKC_ВысотаЛотка");

            if (w1 > 0 && h1 > 0)
            {
                width = w1;
                height = h1;
            }
            else
            {
                // 2️ Если не нашли, пробуем стандартное поле "Размер"
                var sizes = ParseSizePairs(e.LookupParameter("Размер")?.AsString());
                if (sizes.Count > 0)
                {
                    width = sizes[0].W;
                    height = sizes[0].H;
                }
            }

            // 3️ Угол
            angleDeg = GetAngleDeg(e);
        }

        private static int TryGetSizeParam(Element e, string paramName)
        {
            try
            {
                var p = e.LookupParameter(paramName);
                if (p == null) return 0;
                string raw = p.AsValueString() ?? p.AsString() ?? "";
                if (string.IsNullOrWhiteSpace(raw)) return 0;

                raw = raw.Replace("мм", "").Trim();
                raw = raw.Replace(",", ".").Trim();

                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double mm))
                    return (int)Math.Round(mm);

                return 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Разбор параметра "Размер" (ADSK/DKC): "200 ммх100 мм", "200/100-200/100", "200x100".
        /// Возвращает список пар (W,H).
        /// </summary>
        private static List<(int W, int H)> ParseSizePairs(string raw)
        {
            var result = new List<(int, int)>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            // Нормализация
            string Normalize(string s)
            {
                var t = s.Trim();
                t = t.Replace(" ", "");
                t = t.Replace("мм", "");
                t = t.Replace("×", "x");
                t = t.Replace("х", "x");
                t = t.Replace("Х", "x");
                t = t.Replace("/", "x");
                return t;
            }

            // Разделяем по дефисам (для фитингов)
            var segments = raw.Split(new[] { '-', '–', '—' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                var s = Normalize(seg); // например "200x100"
                var wh = s.Split('x');
                if (wh.Length >= 2 &&
                    int.TryParse(wh[0], out int w) &&
                    int.TryParse(wh[1], out int h))
                {
                    result.Add((w, h));
                }
            }

            return result;
        }

        /// <summary>
        /// Извлекает угол (в градусах) из параметра DKC_Angle.
        /// </summary>
        private static double? GetAngleDeg(Element e)
        {
            var p = e.LookupParameter("DKC_Angle");
            if (p == null)
            {
                //Log.Warn("[AngleDebug] LookupParameter('DKC_Angle') = NULL");
                return null;
            }

            try
            {
                //Log.Info($"[AngleDebug] StorageType={p.StorageType}");
                //Log.Info($"[AngleDebug] AsDouble()={p.AsDouble()}");
                //Log.Info($"[AngleDebug] AsValueString()='{p.AsValueString()}'");
                //Log.Info($"[AngleDebug] AsString()='{p.AsString()}'");

                // --- 1️ Если параметр числовой (Revit хранит радианы) ---
                if (p.StorageType == StorageType.Double)
                {
                    double rad = p.AsDouble();
                    if (rad > 0 && rad < Math.PI * 2)
                    {
                        double deg = UnitUtils.ConvertFromInternalUnits(rad, UnitTypeId.Degrees);
                        //Log.Info($"[AngleDebug] Interpreted as radians → {deg:F2}°");
                        return deg;
                    }
                }

                // --- 2️ Если параметр строковый — например "45,00°" или "90" ---
                string text = p.AsValueString() ?? p.AsString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Replace("°", "")
                               .Replace(",", ".")
                               .Trim();

                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double deg))
                    {
                        //Log.Info($"[AngleDebug] Parsed from string → {deg:F2}°");
                        return deg;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[AngleDebug] Exception: {ex.Message}");
            }

            // --- ничего не получилось ---
            Log.Warn("[AngleDebug] Не удалось определить угол.");
            return null;
        }

            private const double FEET_TO_MM = 304.8;

            private static double? GetParamAsFeet(Element e, string paramName)
            {
                var p = e.LookupParameter(paramName);
                if (p == null) return null;
                if (p.StorageType == StorageType.Double)
                    return p.AsDouble();
                return null;
            }

            /// <summary>
            /// Возвращает длину крышки в МИЛЛИМЕТРАХ из любого доступного источника.
            /// Порядок приоритетов:
            /// 1) "Accessories lenght" (часто на 3D)
            /// 2) "Cover lenght"      (часто в спеке)
            /// 3) "Длина"             (общий)
            /// </summary>
            public static double GetCoverLengthMm(Element e)
            {
                double? ft =
                    GetParamAsFeet(e, "Accessories lenght")
                    ?? GetParamAsFeet(e, "Cover lenght")
                    ?? GetParamAsFeet(e, "Длина");

                if (ft.HasValue)
                    return ft.Value * FEET_TO_MM;

                return 0.0;
            }
        }
}

