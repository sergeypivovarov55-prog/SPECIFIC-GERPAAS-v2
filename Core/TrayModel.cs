// Core/TrayModel.cs
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SpecificGerpaas.Models
{
    public static class TrayModel
    {
        public static IEnumerable<Element> GetAll(Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTray)
                .WhereElementIsNotElementType();
            foreach (var e in collector)
                yield return e;
        }
    }
}
