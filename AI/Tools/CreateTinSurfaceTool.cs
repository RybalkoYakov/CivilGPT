using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Surfaces;
using CivilGPT.Infrastructure;
using CivilGPT.Models;

namespace CivilGPT.AI.Tools
{
    /// <summary>
    /// Построение TIN-поверхности в Civil 3D по горизонталям и отметкам.
    /// </summary>
    public class CreateTinSurfaceTool : IPluginTool
    {
        private const string SchemaJson = @"
        {
          ""type"": ""object"",
          ""properties"": {
            ""surfaceName"": {
              ""type"": ""string"",
              ""description"": ""Имя создаваемой поверхности. По умолчанию TIN_<дата_время>.""
            },
            ""source"": {
              ""type"": ""string"",
              ""enum"": [""model"", ""selection""],
              ""description"": ""Откуда брать объекты: model — весь чертёж, selection — выделение.""
            },
            ""midOrdinateDistance"": {
              ""type"": ""number"",
              ""description"": ""Расстояние по срединной ординате, м. По умолчанию 0.1.""
            },
            ""maximumDistance"": {
              ""type"": ""number"",
              ""description"": ""Максимальное расстояние между вершинами, м. По умолчанию 10.""
            },
            ""weedingDistance"": {
              ""type"": ""number"",
              ""description"": ""Расстояние отсеивания вершин, м. По умолчанию 0.5.""
            },
            ""supplementingDistance"": {
              ""type"": ""number"",
              ""description"": ""Дополняющее расстояние для горизонталей, м. По умолчанию 0.5.""
            },
            ""useTexts"": {
              ""type"": ""boolean"",
              ""description"": ""Учитывать текстовые отметки. По умолчанию true.""
            },
            ""useBlocks"": {
              ""type"": ""boolean"",
              ""description"": ""Учитывать блоки-марки. По умолчанию true.""
            },
            ""usePoints"": {
              ""type"": ""boolean"",
              ""description"": ""Учитывать точки AutoCAD. По умолчанию true.""
            }
          },
          ""required"": [""source""]
        }";

        public string Name => "create_tin_surface";

        public string Description =>
            "Создаёт TIN-поверхность Civil 3D по горизонталям (полилинии, линии, сплайны с ненулевой отметкой) " +
            "и по отметкам (тексты, блоки, точки). " +
            "Источник данных: 'model' — всё пространство модели, 'selection' — текущее выделение. " +
            "Используется для получения цифровой модели рельефа из топографического плана.";

        public bool RequiresConfirmation => true;

        public string ParametersSchema => SchemaJson;

        public ToolResult Execute(IDictionary<string, object> args)
        {
            if (!CadApplication.HasActiveDocument)
            {
                return ToolResult.Fail("Нет активного документа AutoCAD.");
            }

            if (!CadApplication.IsCivilAvailable)
            {
                return ToolResult.Fail("Активный документ не является документом Civil 3D.");
            }

            var options = new TinSurfaceOptions
            {
                SurfaceName = JsonHelper.GetString(args, "surfaceName", TinSurfaceOptions.SuggestSurfaceName()),
                MidOrdinateDistance = JsonHelper.TryGetDouble(args, "midOrdinateDistance", out double mid) ? mid : 0.1,
                MaximumDistance = JsonHelper.TryGetDouble(args, "maximumDistance", out double max) ? max : 10.0,
                WeedingDistance = JsonHelper.TryGetDouble(args, "weedingDistance", out double weed) ? weed : 0.5,
                SupplementingDistance = JsonHelper.TryGetDouble(args, "supplementingDistance", out double supp) ? supp : 0.5,
                UseTextsAsElevationMarks = JsonHelper.GetBool(args, "useTexts", true),
                UseBlocksAsElevationMarks = JsonHelper.GetBool(args, "useBlocks", true),
                UsePointsAsElevationMarks = JsonHelper.GetBool(args, "usePoints", true)
            };

            string error;
            if (!options.IsValid(out error))
            {
                return ToolResult.Fail(error);
            }

            bool fromSelection = string.Equals(
                JsonHelper.GetString(args, "source", "model"),
                "selection",
                StringComparison.OrdinalIgnoreCase);

            Document document = CadApplication.ActiveDocument;
            var documentService = new DocumentService(document);

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                SurfaceSourceSet sources;

                if (fromSelection)
                {
                    ObjectId[] selected = GetImpliedSelection();

                    if (selected.Length == 0)
                    {
                        return ToolResult.Fail("Нет выделенных объектов для построения поверхности.");
                    }

                    sources = ContourCollector.Collect(transaction, selected, options);
                }
                else
                {
                    sources = ContourCollector.CollectSpace(
                        transaction, documentService.ModelSpaceId, options);
                }

                if (sources.IsEmpty)
                {
                    return ToolResult.Fail(
                        "Не найдено объектов с отметками. Проверьте, что горизонтали имеют высоту (Z), отличную от нуля.");
                }

                SurfaceBuildResult result;
                try
                {
                    result = TinSurfaceBuilder.Build(document.Database, transaction, sources, options);
                }
                catch (System.Exception ex)
                {
                    return ToolResult.Fail("Не удалось построить поверхность: " + ex.Message);
                }

                transaction.Commit();

                string message = result.ToString();

                if (result.VertexCount == 0)
                {
                    message += " Внимание: поверхность не содержит вершин — проверьте отметки исходных объектов.";
                }

                if (result.ZeroElevationCurves > 0)
                {
                    message += $" Горизонталей с нулевой отметкой: {result.ZeroElevationCurves}.";
                }

                return ToolResult.Ok(message);
            }
        }

        private static ObjectId[] GetImpliedSelection()
        {
            var editor = CadApplication.ActiveEditor;
            if (editor == null) return new ObjectId[0];

            PromptSelectionResult selection = editor.SelectImplied();

            return selection.Status == PromptStatus.OK && selection.Value != null
                ? selection.Value.GetObjectIds()
                : new ObjectId[0];
        }
    }
}
