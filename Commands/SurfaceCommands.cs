using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Surfaces;
using CivilGPT.Models;

namespace CivilGPT.Commands
{
    /// <summary>
    /// РљРѕРјР°РЅРґС‹ РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕРІРµСЂС…РЅРѕСЃС‚РµР№ Civil 3D.
    /// </summary>
    public class SurfaceCommands
    {
        /// <summary>
        /// РџРѕСЃС‚СЂРѕРµРЅРёРµ TIN-РїРѕРІРµСЂС…РЅРѕСЃС‚Рё РїРѕ РіРѕСЂРёР·РѕРЅС‚Р°Р»СЏРј Рё РѕС‚РјРµС‚РєР°Рј.
        /// Enter РЅР° Р·Р°РїСЂРѕСЃРµ РІС‹Р±РѕСЂР° вЂ” РёСЃРїРѕР»СЊР·РѕРІР°С‚СЊ РІСЃС‘ РїСЂРѕСЃС‚СЂР°РЅСЃС‚РІРѕ РјРѕРґРµР»Рё.
        /// </summary>
        [CommandMethod("TINSURFACEFROMCONTOURS")]
        public void CreateTinFromContours()
        {
            EditorService editor = EditorService.FromActiveDocument();

            if (!CadApplication.IsCivilAvailable)
            {
                editor.WriteError("РђРєС‚РёРІРЅС‹Р№ РґРѕРєСѓРјРµРЅС‚ РЅРµ СЏРІР»СЏРµС‚СЃСЏ РґРѕРєСѓРјРµРЅС‚РѕРј Civil 3D.");
                return;
            }

            var options = new TinSurfaceOptions
            {
                SurfaceName = TinSurfaceOptions.SuggestSurfaceName()
            };

            string surfaceName = editor.GetString($"\nРРјСЏ РїРѕРІРµСЂС…РЅРѕСЃС‚Рё <{options.SurfaceName}>");
            if (surfaceName == null)
            {
                editor.WriteLine("РћРїРµСЂР°С†РёСЏ РѕС‚РјРµРЅРµРЅР°.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(surfaceName))
            {
                options.SurfaceName = surfaceName.Trim();
            }

            ObjectId[] selected = editor.GetSelection(
                "\nР’С‹Р±РµСЂРёС‚Рµ РіРѕСЂРёР·РѕРЅС‚Р°Р»Рё Рё РѕС‚РјРµС‚РєРё (Enter вЂ” РІРµСЃСЊ РїСЂРѕСЃС‚СЂР°РЅСЃС‚РІРѕ РјРѕРґРµР»Рё)");

            bool useModelSpace = selected.Length == 0;

            if (useModelSpace)
            {
                if (!editor.AskConfirmation(
                        "\nРћР±СЉРµРєС‚С‹ РЅРµ РІС‹Р±СЂР°РЅС‹. РџРѕСЃС‚СЂРѕРёС‚СЊ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ РїРѕ РІСЃРµРјСѓ РїСЂРѕСЃС‚СЂР°РЅСЃС‚РІСѓ РјРѕРґРµР»Рё?"))
                {
                    editor.WriteLine("РћРїРµСЂР°С†РёСЏ РѕС‚РјРµРЅРµРЅР°.");
                    return;
                }
            }

            string summary =
                $"\nРџРѕРІРµСЂС…РЅРѕСЃС‚СЊ: '{options.SurfaceName}'\n" +
                $"РСЃС‚РѕС‡РЅРёРє: {(useModelSpace ? "РІСЃС‘ РїСЂРѕСЃС‚СЂР°РЅСЃС‚РІРѕ РјРѕРґРµР»Рё" : $"РІС‹Р±СЂР°РЅРѕ РѕР±СЉРµРєС‚РѕРІ: {selected.Length}")}\n" +
                $"РЎСЂРµРґРёРЅРЅР°СЏ РѕСЂРґРёРЅР°С‚Р°: {options.MidOrdinateDistance:F3} Рј, " +
                $"РјР°РєСЃ. СЂР°СЃСЃС‚РѕСЏРЅРёРµ: {options.MaximumDistance:F3} Рј,\n" +
                $"РѕС‚СЃРµРёРІР°РЅРёРµ: {options.WeedingDistance:F3} Рј, " +
                $"РґРѕРїРѕР»РЅРµРЅРёРµ: {options.SupplementingDistance:F3} Рј.";

            if (!editor.AskConfirmation(summary))
            {
                editor.WriteLine("РћРїРµСЂР°С†РёСЏ РѕС‚РјРµРЅРµРЅР°.");
                return;
            }

            Document document = CadApplication.ActiveDocument;
            var documentService = new DocumentService(document);

            try
            {
                using (document.LockDocument())
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    SurfaceSourceSet sources = useModelSpace
                        ? ContourCollector.CollectSpace(transaction, documentService.ModelSpaceId, options)
                        : ContourCollector.Collect(transaction, selected, options);

                    if (sources.IsEmpty)
                    {
                        editor.WriteError(
                            "РќРµ РЅР°Р№РґРµРЅРѕ РѕР±СЉРµРєС‚РѕРІ СЃ РѕС‚РјРµС‚РєР°РјРё. РџСЂРѕРІРµСЂСЊС‚Рµ, С‡С‚Рѕ РіРѕСЂРёР·РѕРЅС‚Р°Р»Рё РёРјРµСЋС‚ РІС‹СЃРѕС‚Сѓ (Z), РѕС‚Р»РёС‡РЅСѓСЋ РѕС‚ РЅСѓР»СЏ.");
                        return;
                    }

                    SurfaceBuildResult result = TinSurfaceBuilder.Build(
                        document.Database, transaction, sources, options);

                    transaction.Commit();

                    editor.WriteLine("--- РџРѕРІРµСЂС…РЅРѕСЃС‚СЊ РїРѕСЃС‚СЂРѕРµРЅР° ---");
                    editor.WriteLine(result.ToString());

                    if (result.ZeroElevationCurves > 0)
                    {
                        editor.WriteLine(
                            $"Р’РЅРёРјР°РЅРёРµ: РіРѕСЂРёР·РѕРЅС‚Р°Р»РµР№ СЃ РЅСѓР»РµРІРѕР№ РѕС‚РјРµС‚РєРѕР№ вЂ” {result.ZeroElevationCurves}.");
                    }

                    if (result.VertexCount == 0)
                    {
                        editor.WriteLine(
                            "Р’РЅРёРјР°РЅРёРµ: РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ РЅРµ СЃРѕРґРµСЂР¶РёС‚ РІРµСЂС€РёРЅ. РџСЂРѕРІРµСЂСЊС‚Рµ РѕС‚РјРµС‚РєРё РёСЃС…РѕРґРЅС‹С… РѕР±СЉРµРєС‚РѕРІ.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteError("РќРµ СѓРґР°Р»РѕСЃСЊ РїРѕСЃС‚СЂРѕРёС‚СЊ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ: " + ex.Message);
            }
        }
    }
}

