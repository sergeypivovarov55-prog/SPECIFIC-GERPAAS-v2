// Utils/ElementCollectorUtils.cs
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SpecificGerpaas.Utils
{
    public static class ElementCollectorUtils
    {
        public static IList<Element> GetCableTrays(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        public static IList<Element> GetFittings(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                .WhereElementIsNotElementType()
                .ToElements();
        }
    }
}
