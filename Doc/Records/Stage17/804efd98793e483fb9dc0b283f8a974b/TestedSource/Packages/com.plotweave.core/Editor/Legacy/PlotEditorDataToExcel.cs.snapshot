using PlotWeave.Authoring;
using PlotWeave.Authoring.Excel;

namespace PlotWeave.Legacy.Editor
{
    public sealed class PlotEditorDataToExcel
    {
        public void Test() => PlotExcelWindow.Open();
        public string[] Export(PlotAuthoringProject project, string directory) => PlotExcelExchange.Export(project, directory);
        public PlotExcelImportPlan InspectImport(PlotAuthoringProject project, string directory) => PlotExcelExchange.PlanImport(project, directory);
    }
}
