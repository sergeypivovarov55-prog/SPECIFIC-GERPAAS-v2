// Commands/CmdExportGerpaasSpec.cs
using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using SpecificGerpaas.Core; // ScheduleExporter

namespace SpecificGerpaas.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdExportGerpaasSpec : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) { message = "Не открыт документ."; return Result.Failed; }

            var doc = uidoc.Document;

            try
            {
                const string scheduleName = "GE_Специфікація кабеленесучих систем";
                var path = ScheduleExporter.ExportScheduleToCsv(doc, scheduleName);
                if (path == null)
                {
                    message = "Спецификация не найдена или экспорт не удался.";
                    return Result.Failed;
                }

                Log.Info("[Export] CSV: " + path);
                TaskDialog.Show("Export", "Експорт виконано:\n" + path);
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
