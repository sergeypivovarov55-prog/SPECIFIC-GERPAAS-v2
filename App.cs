using Autodesk.Revit.UI;

namespace SpecificGerpaas
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            RibbonBuilder.Build(app);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
