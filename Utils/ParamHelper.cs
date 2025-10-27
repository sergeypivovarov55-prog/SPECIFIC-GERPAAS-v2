// -----------------------------------------------------------------------------
// SPECIFIC-GERPAAS : ParamHelper.cs
// -----------------------------------------------------------------------------

using Autodesk.Revit.DB;
using System;
using System.Globalization;

namespace SpecificGerpaas.Utils
{
    public static class ParamHelper
    {
        public static void SetTextParam(Element e, string name, string val)
        {
            var p = e.LookupParameter(name);
            if (p != null && !p.IsReadOnly)
                p.Set(val ?? "");
        }

        public static double GetAngleDeg(Element e)
        {
            // 1. Попробуем получить как число (в радианах)
            var p = e.LookupParameter("DKC_Angle");
            if (p == null) return 0;

            double val = p.AsDouble();
            if (val > 0 && val < Math.PI * 2)
                return val * 180.0 / Math.PI;

            // 2. Если AsDouble() возвращает 0, пробуем строковое значение
            string text = p.AsValueString() ?? "";
            text = text.Replace("°", "").Replace(",", ".").Trim();

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double deg))
                return deg;

            return 0;
        }

        public static int GetIntParam(Element e, string paramName)
        {
            var param = e.LookupParameter(paramName);
            return param?.AsInteger() ?? 0;
        }

        public static double GetDoubleParam(Element e, string paramName)
        {
            var param = e.LookupParameter(paramName);
            return param?.AsDouble() * 304.8 ?? 0; // футы → мм
        }

        public static string GetTextParam(Element e, string paramName)
        {
            var param = e.LookupParameter(paramName);
            return param?.AsString() ?? "";
        }

        public static bool HasParam(Element element, string paramName)
        {
            return element?.LookupParameter(paramName) != null;
        }

        public static void SetNumberParam(Element element, string paramName, double value)
        {
            var param = element?.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Double)
            {
                param.Set(value);
            }
        }

    }
}
