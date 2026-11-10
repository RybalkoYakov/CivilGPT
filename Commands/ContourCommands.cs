using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Contours;

namespace CivilGPT.Commands
{
    /// <summary>
    /// Первая команда: подготовка горизонталей.
    /// Находит линии-горизонтали и их подписи, восстанавливает высоты
    /// и создаёт копии линий в виде полилиний на нужной отметке.
    /// Исходные объекты не изменяются.
    /// </summary>
    public class ContourCommands
    {
        [CommandMethod("CGTINCONTOURS")]
        public void LiftContours()
        {
            EditorService editor = EditorService.FromActiveDocument();

            ObjectId sample = editor.GetEntity(
                "\nВыберите любую горизонталь (по ней определим слой)");

            if (sample.IsNull)
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            Document document = CadApplication.ActiveDocument;

            string layerName;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                var entity = transaction.GetObject(sample, OpenMode.ForRead) as Entity;

                if (entity == null)
                {
                    editor.WriteError("Выбранный объект не является примитивом чертежа.");
                    return;
                }

                layerName = entity.Layer;
            }

            double? tolerance = editor.GetDouble("\nДопуск привязки подписи к линии, м", 1.0);
            if (!tolerance.HasValue)
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            if (!editor.AskConfirmation(
                    $"\nСлой горизонталей: '{layerName}'.\n" +
                    $"Линии и подписи этого слоя будут прочитаны, а копии линий — " +
                    $"помещены на слой '{ContourLiftService.TargetLayerName}' с отметками по подписям.\n" +
                    $"Исходные объекты не изменяются."))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            ContourLiftResult result;

            try
            {
                using (document.LockDocument())
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    result = ContourLiftService.Lift(
                        transaction,
                        document.Database,
                        new DocumentService(document).ModelSpaceId,
                        layerName,
                        tolerance.Value);

                    transaction.Commit();
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteError("Не удалось подготовить горизонтали: " + ex.Message);
                return;
            }

            Report(editor, result, layerName);
        }

        private static void Report(EditorService editor, ContourLiftResult result, string layerName)
        {
            editor.WriteLine("--- Подготовка горизонталей завершена ---");
            editor.WriteLine($"Линий на слое '{layerName}': {result.CurvesFound}");
            editor.WriteLine($"Подписей распознано: {result.LabelsParsed}, привязано к линиям: {result.LabelsMatched}");
            editor.WriteLine($"Создано полилиний на слое '{ContourLiftService.TargetLayerName}': {result.ContoursCreated}");

            if (result.HasResult)
            {
                editor.WriteLine($"Отметки: {result.MinElevation:F2}…{result.MaxElevation:F2} м");
            }

            if (result.Unlabeled > 0)
            {
                editor.WriteLine(
                    $"Внимание: без подписи осталось горизонталей — {result.Unlabeled}. " +
                    "Их высота не назначена, в поверхность они не попадут.");
            }

            if (result.Unresolved > 0)
            {
                editor.WriteLine(
                    $"Внимание: не удалось восстановить высоту у {result.Unresolved} подписей. " +
                    "Нужна хотя бы одна полная отметка (например 205.00) на слое.");
            }

            if (result.Conflicts > 0)
            {
                editor.WriteLine(
                    $"Внимание: спорных привязок — {result.Conflicts} (на одну линию претендовали несколько подписей, " +
                    "оставлена ближайшая).");
            }

            if (!result.HasResult)
            {
                editor.WriteLine("");
                editor.WriteLine("Ни одной полилинии не создано. Проверьте:");
                editor.WriteLine("  — подписи действительно лежат на линиях и на том же слое;");
                editor.WriteLine("  — допуск привязки достаточен (по умолчанию 1 м).");
            }
        }
    }
}
