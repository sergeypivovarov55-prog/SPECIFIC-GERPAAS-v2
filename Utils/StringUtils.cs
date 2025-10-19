using System.Globalization;


namespace SpecificGerpaas.Utils
{
    public static class StringUtils
    {
        public static int? ToInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            if (int.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }


        public static double? ToDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            if (double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }
    }
}