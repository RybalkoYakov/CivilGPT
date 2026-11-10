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
    /// Команды построения поверхностей Civil 3D.
    /// </summary>
    public class SurfaceCommands
    {
        /// <summary>
        /// Построение TIN-поверхности по горизонталям и отметкам.
        /// Enter на запросе выбора — использовать всё пространство модели.
        /// </summary>
        [CommandMethod("TINSURFACEFROMCONTOURS")]
        public void CreateTinFromContours()
        {
            EditorService editor = EditorService.FromActiveDocument();

            if (!CadApplication.IsCivilAvailable)
            {
                editor.WriteError("Активный документ не является документом Civil 3D.");
                return;
            }

            var options = new TinSurfaceOptions
            {
                SurfaceName = TinSurfaceOptions.SuggestSurfaceName()
            };

            string surfaceName = editor.GetString($"\nИмя поверхности <{options.SurfaceName}>");
            if (surfaceName == null)
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(surfaceName))
            {
                options.SurfaceName = surfaceName.Trim();
            }

            ObjectId[] selected = editor.GetSelection(
                "\nВыберите горизонтали и отметки (Enter — всё пространство модели)");

            bool useModelSpace = selected.Length == 0;

            if (useModelSpace)
            {
                if (!editor.AskConfirmation(
                        "\nОбъекты не выбраны. Построить поверхность по всему пространству модели?"))
                {
                    editor.WriteLine("Операция отменена.");
                    return;
                }
            }

            string summary =
                $"\nПоверхность: '{options.SurfaceName}'\n" +
                $"Источник: {(useModelSpace ? "всё пространство модели" : $"выбрано объектов: {selected.Length}")}\n" +
                $"Срединная ордината: {options.MidOrdinateDistance:F3} м, " +
                $"макс. расстояние: {options.MaximumDistance:F3} м,\n" +
                $"отсеивание: {options.WeedingDistance:F3} м, " +
                $"дополнение: {options.SupplementingDistance:F3} м.";

            if (!editor.AskConfirmation(summary))
            {
                editor.WriteLine("Операция отменена.");
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
                            "Не найдено объектов с отметками. Проверьте, что горизонтали имеют высоту (Z), отличную от нуля.");
                        return;
                    }

                    SurfaceBuildResult result = TinSurfaceBuilder.Build(
                        document.Database, transaction, sources, options);

                    transaction.Commit();

                    editor.WriteLine("--- Поверхность построена ---");
                    editor.WriteLine(result.ToString());

                    if (result.ZeroElevationCurves > 0)
                    {
                        editor.WriteLine(
                            $"Внимание: горизонталей с нулевой отметкой — {result.ZeroElevationCurves}.");
                    }

                    if (result.VertexCount == 0)
                    {
                        editor.WriteLine(
                            "Внимание: поверхность не содержит вершин. Проверьте отметки исходных объектов.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteError("Не удалось построить поверхность: " + ex.Message);
            }
        }
    }
}
