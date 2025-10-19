// CmdSyncOrCreateSpecsDkc.cs
using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using SpecificGerpaas.Core; // SpecSynchronizer, TrayModel, FittingModel и т.д.

namespace SpecificGerpaas.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdSyncOrCreateSpecsDkc : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) { message = "Не открыт документ."; return Result.Failed; }

            var doc = uidoc.Document;

            try
            {
                // Запускаем Variant B-поток: заполнение GE_Категорія / GE_Найменування / GE_Артикул
                var sync = new SpecSynchronizer(doc, uiapp);
                sync.Run(); // внутри: Sync() с транзакцией и записью параметров
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
