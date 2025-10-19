// Core/FittingModel.cs
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SpecificGerpaas.Core
{
    public static class FittingModel
    {
        public static IEnumerable<Element> GetAll(Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                .WhereElementIsNotElementType();
            foreach (var e in collector)
                yield return e;
        }
    }
}
