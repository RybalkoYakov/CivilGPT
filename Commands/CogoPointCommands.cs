using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using CivilGPT.CAD.Cogo;
using CivilGPT.CAD.Common;
using CivilGPT.Models;

namespace CivilGPT.Commands
{
    /// <summary>
    /// Команды случайных отклонений точек COGO от проектных значений.
    /// </summary>
    public class CogoPointCommands
    {
        /// <summary>Случайное вертикальное отклонение выделенных точек COGO.</summary>
        [CommandMethod("RANDOMIZEPOINTZ")]
        public void RandomizePointElevation()
        {
            EditorService editor = EditorService.FromActiveDocument();

            if (!CadApplication.IsCivilAvailable)
            {
                editor.WriteError("Активный документ не является документом Civil 3D.");
                return;
            }

            ObjectId[] selected = editor.GetSelection(
                "\nВыберите точки COGO для смещения по Z",
                CogoPointSelector.CogoPointDxfType);

            if (selected.Length == 0)
            {
                editor.WriteLine("Точки не выбраны. Операция отменена.");
                return;
            }

            ElevationOffsetOptions options;
            if (!CommandLineInput.TryGetElevationOffsetOptions(editor, out options))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            string message =
                $"\nБудут изменены высоты {selected.Length} точек COGO.\n" +
                $"Смещение в диапазоне [{options.MinOffset:F3}, {options.MaxOffset:F3}] м.";

            if (!editor.AskConfirmation(message))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            Document document = CadApplication.ActiveDocument;

            using (document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                List<ObjectId> pointIds = CogoPointSelector.FilterCogoPoints(transaction, selected);

                CogoPointElevationService.Result result =
                    CogoPointElevationService.RandomizeElevation(transaction, pointIds, options);

                transaction.Commit();

                editor.WriteLine("--- Смещение высот точек COGO завершено ---");

                if (result.Processed == 0)
                {
                    editor.WriteLine("Не найдено ни одной точки COGO для обработки.");
                    return;
                }

                editor.WriteLine($"Обработано точек: {result.Processed}");
                editor.WriteLine(
                    $"Фактические отклонения: {result.MinAppliedOffset:F3}…{result.MaxAppliedOffset:F3} м");

                if (result.SkippedLocked > 0)
                {
                    editor.WriteLine($"Пропущено заблокированных точек: {result.SkippedLocked}");
                }
            }
        }

        /// <summary>Случайное горизонтальное отклонение всех точек COGO в осях текущей ПСК.</summary>
        [CommandMethod("RANDOMIZEPOINTXY")]
        public void RandomizePointHorizontal()
        {
            EditorService editor = EditorService.FromActiveDocument();

            CivilDocument civilDocument = CadApplication.ActiveCivilDocument;
            if (civilDocument == null)
            {
                editor.WriteError("Активный документ не является документом Civil 3D.");
                return;
            }

            HorizontalOffsetOptions options;
            if (!CommandLineInput.TryGetHorizontalOffsetOptions(editor, out options))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            string message =
                "\nБудут изменены координаты X/Y всех точек COGO в осях текущей ПСК.\n" +
                $"X: [{options.MinOffsetX:F3}, {options.MaxOffsetX:F3}] м, " +
                $"Y: [{options.MinOffsetY:F3}, {options.MaxOffsetY:F3}] м.";

            if (!editor.AskConfirmation(message))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            Matrix3d ucsToWcs = CadApplication.ActiveUcsToWcs;
            Document document = CadApplication.ActiveDocument;

            using (document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                CogoPointHorizontalService.Result result =
                    CogoPointHorizontalService.RandomizeHorizontal(
                        civilDocument, transaction, options, ucsToWcs);

                transaction.Commit();

                editor.WriteLine("--- Горизонтальное смещение точек COGO завершено ---");

                if (result.Processed == 0)
                {
                    editor.WriteLine("Не найдено ни одной точки COGO для обработки.");
                    return;
                }

                editor.WriteLine($"Обработано точек: {result.Processed}");

                if (result.SkippedLocked > 0)
                {
                    editor.WriteLine($"Пропущено заблокированных точек: {result.SkippedLocked}");
                }
            }
        }

        /// <summary>Создание новых точек со случайным отклонением в новой группе точек.</summary>
        [CommandMethod("CREATEDOFFSETPOINTS")]
        public void CreateOffsetPoints()
        {
            EditorService editor = EditorService.FromActiveDocument();

            CivilDocument civilDocument = CadApplication.ActiveCivilDocument;
            if (civilDocument == null)
            {
                editor.WriteError("Активный документ не является документом Civil 3D.");
                return;
            }

            ObjectId[] selected = editor.GetSelection(
                "\nВыберите точки COGO для создания отклонённых копий",
                CogoPointSelector.CogoPointDxfType);

            if (selected.Length == 0)
            {
                editor.WriteLine("Точки не выбраны. Операция отменена.");
                return;
            }

            PointCreationOptions options;
            if (!CommandLineInput.TryGetPointCreationOptions(editor, out options))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            string message =
                $"\nБудет создано {selected.Length} новых точек в группе '{options.GroupName}' " +
                "в осях текущей ПСК.\n" +
                $"X: [{options.MinOffsetX:F3}, {options.MaxOffsetX:F3}] м, " +
                $"Y: [{options.MinOffsetY:F3}, {options.MaxOffsetY:F3}] м, " +
                $"Z: [{options.MinOffsetZ:F3}, {options.MaxOffsetZ:F3}] м.";

            if (!editor.AskConfirmation(message))
            {
                editor.WriteLine("Операция отменена.");
                return;
            }

            Matrix3d ucsToWcs = CadApplication.ActiveUcsToWcs;
            Document document = CadApplication.ActiveDocument;

            using (document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                List<ObjectId> pointIds = CogoPointSelector.FilterCogoPoints(
                    transaction,
                    selected);

                CogoPointCreationService.Result result =
                    CogoPointCreationService.CreateOffsetPoints(
                        civilDocument, transaction, pointIds, options, ucsToWcs);

                transaction.Commit();

                editor.WriteLine("--- Создание точек завершено ---");
                editor.WriteLine($"Создано точек: {result.CreatedCount}");
                editor.WriteLine($"Группа: '{options.GroupName}'");
            }
        }
    }
}
