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
                {
                    // Результат предыдущего запуска иначе наложится на новый.
                    int previous;

                    using (Transaction check = document.Database.TransactionManager.StartTransaction())
                    {
                        previous = ContourLiftService.CountLayerEntities(
                            check,
                            new DocumentService(document).ModelSpaceId,
                            ContourLiftService.TargetLayerName);
                    }

                    if (previous > 0)
                    {
                        if (!editor.AskConfirmation(
                                $"\nНа слое '{ContourLiftService.TargetLayerName}' уже есть объектов: {previous}. " +
                                "Это результат предыдущего запуска. Удалить его?"))
                        {
                            editor.WriteLine("Операция отменена.");
                            return;
                        }
                    }

                    using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                    {
                        if (previous > 0)
                        {
                            ContourLiftService.EraseLayerEntities(
                                transaction,
                                new DocumentService(document).ModelSpaceId,
                                ContourLiftService.TargetLayerName);
                        }

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
            }
            catch (System.Exception ex)
            {
                editor.WriteError("Не удалось подготовить горизонтали: " + ex.Message);
                return;
            }

            Report(editor, result, layerName);
            WriteLog(editor, document, layerName, result);
        }

        /// <summary>
        /// Записать отчёт рядом с чертежом: по нему видно, что именно
        /// произошло с каждой горизонталью.
        /// </summary>
        private static void WriteLog(
            EditorService editor,
            Document document,
            string layerName,
            ContourLiftResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(document.Name)) return;

                string logPath = System.IO.Path.ChangeExtension(document.Name, null)
                                 + "_civilgpt.log";

                var builder = new System.Text.StringBuilder();
                builder.AppendLine("Слой: " + layerName);
                builder.AppendLine("Кривых: " + result.CurvesFound);
                builder.AppendLine("Подписей: " + result.LabelsParsed
                                   + ", привязано: " + result.LabelsMatched
                                   + ", с других слоёв: " + result.LabelsFromOtherLayers);
                builder.AppendLine("Цепочек: " + result.ChainsBuilt
                                   + ", с подписью: " + result.ChainsLabeled
                                   + ", без подписи: " + result.ChainsUnlabeled);
                builder.AppendLine("Не разрешено: " + result.Unresolved);
                builder.AppendLine("Создано полилиний: " + result.ContoursCreated);
                builder.AppendLine();
                builder.AppendLine("X\tY\tПодпись\tПолная\tОтметка\tОтрезков");

                foreach (ContourChainInfo chain in result.Chains)
                {
                    builder.AppendLine(string.Join("\t", new[]
                    {
                        chain.X.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        chain.Y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        chain.LabelText ?? "(нет)",
                        chain.IsFull ? "да" : "нет",
                        chain.Elevation.HasValue
                            ? chain.Elevation.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                            : "-",
                        chain.Segments.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }));
                }

                System.IO.File.WriteAllText(
                    logPath, builder.ToString(), new System.Text.UTF8Encoding(false));

                editor.WriteLine($"Подробный отчёт: {logPath}");
            }
            catch (System.Exception ex)
            {
                editor.WriteLine("Не удалось записать отчёт: " + ex.Message);
            }
        }

        private static void Report(EditorService editor, ContourLiftResult result, string layerName)
        {
            editor.WriteLine("--- Подготовка горизонталей завершена ---");
            editor.WriteLine($"Кривых на слое '{layerName}': {result.CurvesFound}");
            editor.WriteLine($"Подписей распознано: {result.LabelsParsed}, привязано к линиям: {result.LabelsMatched}");

            if (result.LabelsFromOtherLayers > 0)
            {
                editor.WriteLine($"Из них взято с других слоёв: {result.LabelsFromOtherLayers}.");
            }
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

                if (result.CurvesFound == 0)
                {
                    editor.WriteLine(
                        $"На слое '{layerName}' нет кривых — выбранный объект относится к другому слою.");
                }
                else if (result.LabelsParsed == 0)
                {
                    editor.WriteLine(
                        "Подписей горизонталей не найдено ни на одном слое. " +
                        "Проверьте, что подписи — тексты или атрибуты блоков, а не мультивыноски.");
                }
                else
                {
                    editor.WriteLine("Ни одной полилинии не создано. Проверьте:");
                    editor.WriteLine("  — подписи лежат рядом с линиями;");
                    editor.WriteLine("  — допуск привязки достаточен (по умолчанию 1 м).");
                }
            }
            else if (result.ContoursCreated * 5 < result.CurvesFound)
            {
                editor.WriteLine("");
                editor.WriteLine(
                    $"Внимание: отрисовано {result.ContoursCreated} из {result.CurvesFound} кривых. " +
                    "Похоже, подписи относятся к другому листу или слою — проверьте, " +
                    "что выбрана горизонталь нужного листа.");
            }
        }
    }
}
