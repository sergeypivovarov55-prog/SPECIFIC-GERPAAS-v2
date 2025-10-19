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

    }
}
