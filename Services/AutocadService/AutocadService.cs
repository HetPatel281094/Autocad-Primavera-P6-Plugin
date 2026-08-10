using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;


using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;
using System.Xml;
using System.Diagnostics;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{
    public partial class AutocadService
    {
        public AutocadService() { }
        public List<BlockReference> GetPluginBlockImpliedSelected(AcadAppServ.Document doc)
        {
            try
            {
                var _activeDoc = doc;
                var _ed = _activeDoc.Editor;
                var _db = _activeDoc.Database;
                var _selected = _ed.SelectImplied();
                var _selSet = _selected.Value;

                if (_selected.Status != PromptStatus.OK || _selected.Value == null)
                {
                    _ed.WriteMessage("\nNo objects were pre-selected.\n");
                    return null;
                }

                List<BlockReference> _blockRefs = new List<BlockReference> { };

                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in _selSet)
                    {
                        if (selObj == null) continue;

                        if (selObj.ObjectId.ObjectClass.DxfName == "INSERT")
                        {
                            BlockReference blockRef = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;

                            var attDefDict = PluginBlockRefAutocadHelpers.Get_AttRefDict(blockRef, tr);

                            if (blockRef == null) { continue; };
                            if (!PluginBlockRefAutocadHelpers.IsValidPluginBlock(attDefDict)){ continue;  };

                            _blockRefs.Add(blockRef);

                        }
                    }
                }

                return _blockRefs;

            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                return new List<BlockReference>();
            }
        }

    }

}