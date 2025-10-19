using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace SpecificGerpaas.Core
{
    public static class RevitUtils
    {
        public static void Info(string msg) => TaskDialog.Show("SPECIFIC-GERPAAS", msg);
    }
}