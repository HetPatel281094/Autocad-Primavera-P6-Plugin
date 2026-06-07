// (C) Copyright 2026 by  
//
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(Autocad_Primavera_P6_Plugin.MyCommands))]

namespace Autocad_Primavera_P6_Plugin
{

    public partial class MyCommands
    {
        // 1. P6InsertBlocks
        [CommandMethod("P6InsertBlocks")]
        public void P6InsertBlocks()
        {
            MyPlugin _pluginInstance = MyPlugin.Instance;
            _pluginInstance.P6InsertBlocks();
        }

        // 2. 
        [CommandMethod("P6Command2")]
        public void MyPickFirst()
        {
            MyPlugin _pluginInstance = MyPlugin.Instance;
        }

        // 3. 
        [CommandMethod("P6Command3")]
        public void MySessionCmd()
        {
            MyPlugin _pluginInstance = MyPlugin.Instance;
        }

        [CommandMethod("PRECAST_NEW")]
        public void CreatePrecastItem()
        {
            // 'Document' represents the current DWG file open in the editor.
            // 'Database' is the underlying data structure (the 'engine') of the drawing.
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Transactions are 'Safeguards'. If the code crashes halfway, the drawing 
            // won't be corrupted because nothing is saved until tr.Commit() is called.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // --- 1. SELECTION PHASE ---

                // PromptEntityOptions configures what the user is allowed to click.
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect existing Precast Block to copy properties from: ");
                peo.SetRejectMessage("\nError: You must select an AutoCAD Block.");
                peo.AddAllowedClass(typeof(BlockReference), true); // Filters out Lines, Circles, etc.

                PromptEntityResult res = ed.GetEntity(peo);
                if (res.Status != PromptStatus.OK) return;

                // Open the selected object. 'OpenMode.ForRead' is memory efficient when 
                // you only need to look at data without changing the original.
                BlockReference templateRef = tr.GetObject(res.ObjectId, OpenMode.ForRead) as BlockReference;

                // --- 2. ID GENERATION PHASE ---

                // This is your 'Activity Code'. In a real app, this method would 
                // query a database or the Drawing's 'Named Object Dictionary'.
                string nextActivityCode = "ACT-" + GenerateNewActivityID();

                // --- 3. POSITIONING PHASE ---

                // Ask the user where to place the 'New' precast item.
                PromptPointResult ppr = ed.GetPoint("\nPick insertion point for new Precast Item: ");
                if (ppr.Status != PromptStatus.OK) return;

                // --- 4. CLONING & INSTANTIATION ---

                // 'BlockTableRecord' is the 'Blueprint' of the block.
                // 'BlockReference' is the 'Instance' (the physical object on the screen).
                // Here, we get the blueprint ID from our template to make an exact copy.
                ObjectId blockDefId = templateRef.BlockTableRecord;

                // Create the new instance in memory at the user's chosen coordinates.
                BlockReference newRef = new BlockReference(ppr.Value, blockDefId);

                // Access 'Model Space'. This is the 'container' that holds all visible geometry.
                // We must open it 'ForWrite' to add our new block to the drawing.
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                // Physically add the block to the database.
                modelSpace.AppendEntity(newRef);
                tr.AddNewlyCreatedDBObject(newRef, true); // Tells the Transaction to track this object.

                // --- 5. ATTRIBUTE LOGIC (The "Partial Properties" part) ---

                // Step A: We need the definitions (tags) from the original blueprint.
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    // We are looking for 'AttributeDefinitions' (the template for text fields).
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead);
                    AttributeDefinition attDef = obj as AttributeDefinition;

                    // Only process if it's a real attribute and isn't 'Constant' (unchangeable).
                    if (attDef != null && !attDef.Constant)
                    {
                        // Create a new 'AttributeReference' (the actual text attached to the new block).
                        using (AttributeReference attRef = new AttributeReference())
                        {
                            // Initilize the text properties (font, layer, etc.) based on the blueprint.
                            attRef.SetAttributeFromBlock(attDef, newRef.BlockTransform);

                            // LOGIC GATE: 
                            // If the tag is our ID tag, use the auto-generated Activity Code.
                            // Otherwise, copy the 'Partial Property' value from the selected block.
                            if (attRef.Tag.Equals("ACTIVITY_ID", System.StringComparison.OrdinalIgnoreCase))
                            {
                                attRef.TextString = nextActivityCode;
                            }
                            else
                            {
                                // Helper method pulls the text from the original block's matching tag.
                                attRef.TextString = GetValueFromTemplate(templateRef, attRef.Tag, tr);
                            }

                            // Attach the filled-out text to our new block instance.
                            newRef.AttributeCollection.AppendAttribute(attRef);
                            tr.AddNewlyCreatedDBObject(attRef, true);
                        }
                    }
                }

                // Save all changes to the drawing. 
                // If we don't call this, the new block vanishes when the method ends.
                tr.Commit();
                ed.WriteMessage($"\nSuccess: Created {nextActivityCode} with partial properties.");
            }
        }

        /// <summary>
        /// Searches the template block for a specific Attribute Tag and returns its value.
        /// This is how "Partial Properties" are carried over.
        /// </summary>
        private string GetValueFromTemplate(BlockReference template, string tag, Transaction tr)
        {
            // Iterate through all attributes currently attached to the 'Old' block.
            foreach (ObjectId id in template.AttributeCollection)
            {
                AttributeReference att = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
                if (att != null && att.Tag.Equals(tag, System.StringComparison.OrdinalIgnoreCase))
                {
                    return att.TextString; // Found the matching property!
                }
            }
            return ""; // Return empty if property doesn't exist on the original.
        }

        private int GenerateNewActivityID()
        {
            // Placeholder: In production, you'd pull this from a Counter 
            // stored in XData or a SQL database.
            return new System.Random().Next(1000, 9999);
        }

    }

}
