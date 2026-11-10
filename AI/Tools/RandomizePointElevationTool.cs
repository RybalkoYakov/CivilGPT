using System;
using System.Collections.Generic;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivilGPT.CAD.Cogo;
using CivilGPT.CAD.Common;
using CivilGPT.Models;

namespace CivilGPT.AI.Tools
{
    /// <summary>
    /// Случайное вертикальное отклонение точек COGO от проектных отметок.
    /// </summary>
    public class RandomizePointElevationTool : IPluginTool
    {
        public string Name => "randomize_point_elevation";

        public string Description =>
            "Применяет случайное смещение по высоте (Z) к точкам COGO. " +
            "По умолчанию работает с текущим выделением в AutoCAD. " +
            "Можно явно указать номера точек через параметр 'pointNumbers'. " +
            "Используется для имитации погрешности измерений относительно проектных значений.";

        public bool RequiresConfirmation => true;

        public JsonElement ParametersSchema => JsonDocument.Parse(@"
        {
          ""type"": ""object"",
          ""properties"": {
            ""minOffset"": {
              ""type"": ""number"",
              ""description"": ""Минимальное смещение по Z в метрах. Может быть отрицательным.""
            },
            ""maxOffset"": {
              ""type"": ""number"",
              ""description"": ""Максимальное смещение по Z в метрах. Должно быть больше или равно minOffset.""
            },
            ""pointNumbers"": {
              ""type"": ""array"",
              ""items"": { ""type"": ""integer"" },
              ""description"": ""Опциональный список номеров точек COGO. Если не указан — используются выделенные точки.""
            }
          },
          ""required"": [""minOffset"", ""maxOffset""]
        }").RootElement;

        public ToolResult Execute(JsonElement args)
        {
            if (!CadApplication.HasActiveDocument)
            {
                return ToolResult.Fail("Нет активного документа AutoCAD.");
            }

            CivilDocument civilDocument = CadApplication.ActiveCivilDocument;
            if (civilDocument == null)
            {
                return ToolResult.Fail("Активный документ не является документом Civil 3D.");
            }

            double minOffset;
            if (!ToolArguments.TryGetDouble(args, "minOffset", out minOffset))
            {
                return ToolResult.Fail("Не указан или некорректен параметр 'minOffset'.");
            }

            double maxOffset;
            if (!ToolArguments.TryGetDouble(args, "maxOffset", out maxOffset))
            {
                return ToolResult.Fail("Не указан или некорректен параметр 'maxOffset'.");
            }

            var options = new ElevationOffsetOptions
            {
                MinOffset = minOffset,
                MaxOffset = maxOffset
            };

            string error;
            if (!options.IsValid(out error))
            {
                return ToolResult.Fail(error);
            }

            Document document = CadApplication.ActiveDocument;

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                List<ObjectId> pointIds;
                try
                {
                    pointIds = ResolveTargetPoints(transaction, civilDocument, args);
                }
                catch (Exception ex)
                {
                    return ToolResult.Fail(ex.Message);
                }

                if (pointIds.Count == 0)
                {
                    return ToolResult.Fail(
                        "Не выбрано ни одной точки COGO. Выделите точки в AutoCAD или укажите параметр 'pointNumbers'.");
                }

                CogoPointElevationService.Result result =
                    CogoPointElevationService.RandomizeElevation(transaction, pointIds, options);

                transaction.Commit();

                if (result.Processed == 0)
                {
                    return ToolResult.Fail("Не найдено ни одной подходящей точки COGO.");
                }

                string message =
                    $"Обработано точек: {result.Processed}. " +
                    $"Фактические отклонения: {result.MinAppliedOffset:F3}…{result.MaxAppliedOffset:F3} м.";

                if (result.SkippedLocked > 0)
                {
                    message += $" Пропущено заблокированных: {result.SkippedLocked}.";
                }

                return ToolResult.Ok(message);
            }
        }

        /// <summary>
        /// Точки для обработки: явный список номеров приоритетнее текущего выделения.
        /// </summary>
        private static List<ObjectId> ResolveTargetPoints(
            Transaction transaction,
            CivilDocument civilDocument,
            JsonElement args)
        {
            List<uint> numbers;

            if (ToolArguments.TryGetUIntList(args, "pointNumbers", out numbers))
            {
                List<ObjectId> found = CogoPointSelector.ByNumbers(transaction, civilDocument, numbers);

                if (found.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Ни одна из указанных точек не найдена в чертеже: " +
                        string.Join(", ", numbers) + ".");
                }

                return found;
            }

            ObjectId[] selected = CogoPointSelector.GetImpliedPointIds(CadApplication.ActiveEditor);

            if (selected.Length == 0)
            {
                throw new InvalidOperationException(
                    "Нет выделенных точек COGO. Выделите точки в AutoCAD или укажите параметр 'pointNumbers'.");
            }

            List<ObjectId> cogoPoints = CogoPointSelector.FilterCogoPoints(transaction, selected);

            if (cogoPoints.Count == 0)
            {
                throw new InvalidOperationException("Среди выделенных объектов нет ни одной точки COGO.");
            }

            return cogoPoints;
        }
    }
}
