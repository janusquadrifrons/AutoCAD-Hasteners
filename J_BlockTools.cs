﻿using System;
using System.IO; // File Management
using System.Text.RegularExpressions; // Regex 

/// Block related operators

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;

namespace J_Tools
{
    public class J_BlockTools
    {
        //  Get entity's layer properties, and assign to entity itself in order to shorten Layer Table.

        [CommandMethod("BLOCKSIMPLFY")]
        static public void BlockSimplfy()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peopt = new PromptEntityOptions("\nSelect block reference");
            peopt.SetRejectMessage("\nSelect only block reference");
            peopt.AddAllowedClass(typeof(BlockReference), false);
            peopt.AllowNone = false;

            PromptEntityResult peres = ed.GetEntity(peopt);
            if (peres != null)
            {
                try
                {
                    using (Transaction tx = db.TransactionManager.StartTransaction())
                    {
                        BlockReference bref = tx.GetObject(peres.ObjectId, OpenMode.ForRead) as BlockReference;
                        BlockTableRecord btrec = null;

                        LayerTable laytab = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                        LayerTableRecord laytabrec = null;

                        if (bref.IsDynamicBlock) { btrec = tx.GetObject(bref.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord; }
                        else { btrec = tx.GetObject(bref.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord; }

                        if (btrec != null) { ed.WriteMessage("Block name is : " + btrec.Name + "\n"); }

                        foreach (ObjectId oid in btrec)
                        {
                            Entity en = tx.GetObject(oid, OpenMode.ForWrite) as Entity;
                            //DBObject dbObj = tx.GetObject(oid, OpenMode.ForRead) as DBObject;

                            ed.WriteMessage("\nType : " + en.GetType().Name);

                            if (en is Dimension) { ed.WriteMessage("\nDimension detected..." + en); continue; }
                            else
                            {
                                // Get entity properties
                                int en_clr = en.ColorIndex;
                                string en_lt = en.Linetype;
                                LineWeight en_lw = en.LineWeight;
                                ed.WriteMessage("\nEntity color/linetype/lineweight is : " + en_clr + "/" + en_lt + "/" + en_lw + "/n");

                                // Get entity's layer properties
                                laytabrec = tx.GetObject(laytab[en.Layer], OpenMode.ForRead) as LayerTableRecord;
                                Color laytabrec_clr = laytabrec.Color;
                                ObjectId laytabrec_lt_oid = laytabrec.LinetypeObjectId;
                                LinetypeTableRecord ltrec = tx.GetObject(laytabrec_lt_oid, OpenMode.ForRead) as LinetypeTableRecord;
                                string laytabrec_lt_name = ltrec.Name;
                                LineWeight laytabrec_lw = laytabrec.LineWeight;
                                ed.WriteMessage("\nEntity layers color/linetype/lineweight is : " + laytabrec_clr + "/" + laytabrec_lt_name + "/" + laytabrec_lw);

                                // Final setting
                                if (en_clr == 256) { en.Color = laytabrec_clr; }
                                if (en_lt == "ByLayer") { en.Linetype = laytabrec_lt_name; }
                                if (en_lw == LineWeight.ByLayer) { en.LineWeight = laytabrec_lw; }

                                en.Layer = "0";
                            }
                        }
                        ed.Regen();
                        tx.Commit();
                    }
                }

                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Application.ShowAlertDialog("Exception occured : \n" + ex.Message);
                }
            }

        }

        /////////////////////////////////////////////////////////

        // Wblock all blocks for optimisation/inspection
        // ISSUE : Replace dll path with active document path

        [CommandMethod("SEPARATEBLOCKS")]
        static public void SeparateBlocks()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            string path = "blocks"; //--will be used to create sub folder
            RXClass blockclass = RXClass.GetClass(typeof(BlockReference)); //--will be used to determine enttiy type

            // Determine if the path exists > Create the directory
            if (Directory.Exists(path))
            {
                ed.WriteMessage("\nThat path exists already.");
            }
            else
            {
                DirectoryInfo diri = Directory.CreateDirectory(path);
                ed.WriteMessage("\nThe directory created successfully.");
            }

            // Iterating Blocks > Wblock to path
            using (Transaction tx = db.TransactionManager.StartTransaction())
            { 
                BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btrec = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                foreach (ObjectId oid in btrec)
                {
                    Entity ent = tx.GetObject(oid, OpenMode.ForRead) as Entity;

                    // way 1 : just keeping for further use
                    if(oid.ObjectClass == blockclass)
                    {
                        ed.WriteMessage("\nIts a Block..."); 
                    }
                    // way 2 : in action
                    if (ent is BlockReference)
                    {
                        ed.WriteMessage("\nReference...");

                        // Object handling
                        BlockReference bref = tx.GetObject(oid, OpenMode.ForRead) as BlockReference;

                        // Casting obj name to string > make anonymous block/illegal names suitable > check on console 
                        string bref_name = bref.Name;
                         
                        Regex illegalInFileName = new Regex(@"[\\/:*?""<>|]");
                        bref_name = illegalInFileName.Replace(bref_name, "_");

                        ed.WriteMessage("\n" + bref_name);

                        // Put obj in local collection
                        ObjectIdCollection oidcoll = new ObjectIdCollection();
                        oidcoll.Add(oid);

                        // Clone database and wblock
                        using (Database newdb = new Database(true, false))
                        {
                            db.Wblock(newdb, oidcoll, Point3d.Origin, DuplicateRecordCloning.Ignore);
                            string filename = path + "\\" + bref_name + ".dwg";
                            newdb.SaveAs(filename, DwgVersion.Newest);
                        }
                    }
                }
                
                tx.Commit();
            }
        }


    }
}
