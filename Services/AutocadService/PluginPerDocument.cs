using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Document;


[assembly: PerDocumentClass(typeof(Autocad_Primavera_P6_Plugin.Services.AutocadService.PluginPerDocument))]

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{
    public class PluginPerDocument : IDisposable
    {
        private bool _disposed;
        private bool _processing;

        private MyPlugin _pluginInstance;
        private AcadDocument _doc;
        private Database _db => _doc.Database;

        private readonly HashSet<ObjectId> _dirtyIds = new();

        public Project _p6Project;
        public Dictionary<ObjectId, PlugInBlockReference> _pluginBlockRefDict = new();

        public PluginPerDocument(AcadDocument doc)
        {
            _doc = doc;

            _doc.UserData["MyPluginPerDocumentClass"] = this;

            _pluginInstance = MyPlugin.Instance;

            _ = Reinitialize();
        }

        private void OnLoginStatusChanged(object sender, LoginStatusChangedEventArgs e)
        {
            _pluginInstance.MyP6ApiService.LoginStatusChanged -= OnLoginStatusChanged;
            _ = Reinitialize();
        }

        public async Task Reinitialize()
        {
            _disposed = false;
            _processing = false;

            _dirtyIds.Clear();

            if (_doc == null || _pluginInstance == null) { return; }

            if (!_pluginInstance.MyP6ApiService.IsLoggedIn)
            {
                _pluginInstance.MyP6ApiService.LoginStatusChanged -= OnLoginStatusChanged;
                _pluginInstance.MyP6ApiService.LoginStatusChanged += OnLoginStatusChanged;
                return;
            }

            _p6Project = await _pluginInstance.MyP6ApiService.GetP6ProjectFromDWGFile(_doc);

            if (_p6Project == null) { return; }

            await ReinitializeDict();

            SubscribeToEvents();
        }

        public async Task ReinitializeDict()
        {
            _pluginBlockRefDict.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                RXClass blockReferenceClass = RXObject.GetClass(typeof(BlockReference));

                var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(_db), OpenMode.ForRead);

                var matchingBlocks = modelSpace
                    .Cast<ObjectId>()
                    .Where(id => id.ObjectClass.IsDerivedFrom(blockReferenceClass))
                    .Select(id => (BlockReference)tr.GetObject(id, OpenMode.ForRead))
                    .Where(br =>
                        br.AttributeCollection
                            .Cast<ObjectId>()
                            .Select(id => (AttributeReference)tr.GetObject(id, OpenMode.ForRead))
                            .Any(att =>
                                att.Tag.Equals(
                                    "attribute_check_string",
                                    StringComparison.OrdinalIgnoreCase) &&
                                att.TextString.Equals(
                                    "FoundOK",
                                    StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                matchingBlocks.ForEach(
                    br => {
                        var pluginBR = new PlugInBlockReference(_pluginInstance, _doc, br, tr);
                        _ = pluginBR.AsyncInit();
                        _pluginBlockRefDict.Add(br.ObjectId, pluginBR);
                    });
            }

        }

        private void SubscribeToEvents()
        {
            // idempotent unsubscribe before subscribing
            _db.ObjectAppended -= OnObjectAppended;
            _db.ObjectModified -= OnObjectModified;
            _db.ObjectErased -= OnObjectErased;
            _db.ObjectUnappended -= OnObjectUnappended;
            _db.ObjectReappended -= OnObjectReappended;

            _doc.Editor.EnteringQuiescentState -= OnEnteringQuiescentState;

            // Attach events
            _db.ObjectAppended += OnObjectAppended;
            _db.ObjectModified += OnObjectModified;
            _db.ObjectErased += OnObjectErased;
            _db.ObjectUnappended += OnObjectUnappended;
            _db.ObjectReappended += OnObjectReappended;

            _doc.Editor.EnteringQuiescentState += OnEnteringQuiescentState;
        }

        private void MarkDirty(DBObject obj) { if (obj is BlockReference) { _dirtyIds.Add(obj.ObjectId); } }

        private void OnObjectAppended(object sender, ObjectEventArgs e) => MarkDirty(e.DBObject);

        private void OnObjectModified(object sender, ObjectEventArgs e) => MarkDirty(e.DBObject);

        private void OnObjectErased(object sender, ObjectErasedEventArgs e) => MarkDirty(e.DBObject);

        private void OnObjectUnappended(object sender, ObjectEventArgs e) => MarkDirty(e.DBObject);

        private void OnObjectReappended(object sender, ObjectEventArgs e) => MarkDirty(e.DBObject);

        private void OnEnteringQuiescentState(object sender, EventArgs e) => ProcessPendingChanges();

        private void ProcessPendingChanges()
        {
            if (_processing || _dirtyIds.Count == 0) return;

            _processing = true;

            try
            {
                using var tr = _db.TransactionManager.StartOpenCloseTransaction();

                RXClass blockReferenceClass = RXObject.GetClass(typeof(BlockReference));

                ObjectId modelSpaceId = SymbolUtilityServices.GetBlockModelSpaceId(_db);

                foreach (ObjectId id in _dirtyIds.ToArray())
                {
                    if (!id.IsValid) { RemoveFromDictionary(id); continue; }

                    if (id.IsErased) { RemoveFromDictionary(id); continue; }

                    if (!id.ObjectClass.IsDerivedFrom(blockReferenceClass)) { RemoveFromDictionary(id); continue; }

                    // Fetch Actual block to verify OwnerId
                    var blockRef = (BlockReference)tr.GetObject(id, OpenMode.ForRead);

                    if (blockRef.OwnerId != modelSpaceId) { RemoveFromDictionary(id); continue; }

                    // Verify its a Plugin BlockReference
                    var has_attribute_check_string_FoundOK = blockRef
                        .AttributeCollection
                        .Cast<ObjectId>()
                        .Select(id => (AttributeReference)tr.GetObject(id, OpenMode.ForRead))
                        .Any(att =>
                            att.Tag.Equals("attribute_check_string", StringComparison.OrdinalIgnoreCase) &&
                            att.TextString.Equals("FoundOK", StringComparison.OrdinalIgnoreCase)
                        );

                    if (!has_attribute_check_string_FoundOK) { RemoveFromDictionary(id); continue; }

                    Synchronize(blockRef, tr);
                }

                tr.Commit();

                _dirtyIds.Clear();
            }
            finally
            {
                _processing = false;
            }

        }

        private void RemoveFromDictionary(ObjectId id)
        {
            if (_pluginBlockRefDict.Remove(id))
            {
                Debug.Print($"Removed block reference {id} from dictionary");
            }
        }

        private void Synchronize(BlockReference br, Transaction tr)
        {
            if (!_pluginBlockRefDict.ContainsKey(br.ObjectId))
            {
                var pluginBR = new PlugInBlockReference(_pluginInstance, _doc, br, tr);
                _ = pluginBR.AsyncInit();
                _pluginBlockRefDict.Add(br.ObjectId, pluginBR);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _db.ObjectAppended -= OnObjectAppended;
            _db.ObjectModified -= OnObjectModified;
            _db.ObjectErased -= OnObjectErased;
            _db.ObjectUnappended -= OnObjectUnappended;
            _db.ObjectReappended -= OnObjectReappended;

            _doc.Editor.EnteringQuiescentState -= OnEnteringQuiescentState;

            _dirtyIds.Clear();
            _disposed = true;
        }

    }
}
