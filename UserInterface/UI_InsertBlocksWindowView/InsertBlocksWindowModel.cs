using System.Threading.Tasks;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;

using Autodesk.AutoCAD.DatabaseServices;
using System;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using System.Linq;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{

    public sealed class PredefinedBlockInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class InsertBlocksWindowModel
    {
        public MyPlugin PluginInstance = null;
        public P6ApiService MyP6ApiService => PluginInstance.MyP6ApiService;
        public AutocadService MyAutocadService => PluginInstance.MyAutocadService;

        public Document ActAcadDoc = null;
        public Project CurrentProject = null;
        public PlugInBlockReference PreselectedBlock = null;

        public string MoveInfoXPropertyName = "MoveInfo X";
        public string MoveInfoYPropertyName = "MoveInfo Y";

        public async Task Async_Init()
        {
            ActAcadDoc = AcadApp.DocumentManager.MdiActiveDocument;
            CurrentProject = await MyP6ApiService.GetP6ProjectFromDWGFile(ActAcadDoc);

            var _selectedBlocks = MyAutocadService.GetPluginBlockImpliedSelected(ActAcadDoc);
            var _selectedBlock = (_selectedBlocks != null && _selectedBlocks.Count > 0) ? _selectedBlocks.Last() : null;

            if (_selectedBlock == null) { return; };

            using(var tr = ActAcadDoc.TransactionManager.StartTransaction())
            {
                PreselectedBlock = new PlugInBlockReference(PluginInstance, ActAcadDoc, _selectedBlock, tr);
            }

            return;
        }

    }

}
