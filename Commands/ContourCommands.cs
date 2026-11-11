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
    /// Находит линии-горизонтали и их подписи (в том числе в атрибутах блоков),
    /// сшивает отрезки одной горизонтали, восстанавливает высоты
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
                    $"Все кривые этого слоя будут сшиты в цельные горизонтали, " +
                    $"подписи прочитаны из текстов и атрибутов блоков,\n" +
                    $"а копии линий помещены на слой '{ContourLiftService.TargetLayerName}' " +
                    $"с вычисленными отметками.\n" +
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
                        tolerance.Value,
                        ContourLiftService.DefaultGap,
                        ContourLiftService.DefaultLateralTolerance,
                        ContourLiftService.DefaultAngleDegrees);

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
            editor.WriteLine($"Кривых на слое '{layerName}': {result.CurvesFound}");
            editor.WriteLine($"Подписей распознано: {result.LabelsParsed}, привязано к линиям: {result.LabelsMatched}");
            editor.WriteLine(
                $"Собрано горизонталей из отрезков: {result.ChainsBuilt} " +
                $"(с подписью {result.ChainsLabeled}, без подписи {result.ChainsUnlabeled})");
            editor.WriteLine($"Создано полилиний на слое '{ContourLiftService.TargetLayerName}': {result.ContoursCreated}");

            if (result.HasResult)
            {
                editor.WriteLine($"Отметки: {result.MinElevation:F2}…{result.MaxElevation:F2} м");
            }

            if (result.Unresolved > 0)
            {
                editor.WriteLine(
                    $"Внимание: не удалось восстановить высоту у {result.Unresolved} горизонталей. " +
                    "Нужна хотя бы одна полная отметка (например 205.00) на слое.");
            }

            if (result.Conflicts > 0)
            {
                editor.WriteLine(
                    $"Внимание: спорных привязок — {result.Conflicts} " +
                    "(на один отрезок претендовали несколько подписей, оставлена полная или ближайшая).");
            }

            if (result.ChainsWithDifferentLabels > 0)
            {
                editor.WriteLine(
                    $"Внимание: цепочек с разными подписями — {result.ChainsWithDifferentLabels}. " +
                    "Это признак ошибки сшивки, сообщите разработчику.");
            }

            if (!result.HasResult)
            {
                editor.WriteLine("");
                editor.WriteLine("Ни одной полилинии не создано. Проверьте:");
                editor.WriteLine("  — подписи лежат на слое и рядом с линиями;");
                editor.WriteLine("  — допуск привязки достаточен (по умолчанию 1 м).");
            }
        }
    }
}
