using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Contours;
using CivilGPT.CAD.Surfaces;
using CivilGPT.Models;

namespace CivilGPT.Commands
{
    /// <summary>
    /// Вторая команда: сборка TIN-поверхности.
    /// Создаёт поверхность по подготовленным горизонталям (или дополняет существующую),
    /// добавляет характерные линии как брейклайны и внешнюю границу участка.
    /// </summary>
    public class SurfaceCommands
    {
        [CommandMethod("CGTINSURFACE")]
        public void BuildSurface()
        {
            EditorService editor = EditorService.FromActiveDocument();

            if (!CadApplication.IsCivilAvailable)
            {
                editor.WriteError("Активный документ не является документом Civil 3D.");
                return;
            }

            Document document = CadApplication.ActiveDocument;
            var documentService = new DocumentService(document);

            // 1. Куда добавлять: существующая поверхность или новая
            ObjectId surfaceId = editor.GetEntity(
                "\nВыберите поверхность TIN (Enter — построить новую)");

            // 2. Характерные линии
            ObjectId[] breaklineIds = editor.GetSelection(
                "\nВыберите характерные линии: бровки, лотки, уступы (Enter — пропустить)");

            // 3. Граница участка
            ObjectId boundaryId = editor.GetEntity(
                "\nВыберите замкнутую границу участка (Enter — без границы)");

            string surfaceName = null;
            if (surfaceId.IsNull)
            {
                surfaceName = editor.GetString(
                    $"\nИмя новой поверхности <{TinSurfaceOptions.SuggestSurfaceName()}>");

                if (surfaceName == null)
                {
                    editor.WriteLine("Операция отменена.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(surfaceName))
                {
                    surfaceName = TinSurfaceOptions.SuggestSurfaceName();
                }
            }

            if (breaklineIds.Length == 0 && boundaryId.IsNull && surfaceId.IsNull)
            {
                if (!editor.AskConfirmation(
                        $"\nХарактерные линии и граница не выбраны.\n" +
                        $"Будет создана поверхность '{surfaceName}' только по горизонталям слоя " +
                        $"'{ContourLiftService.TargetLayerName}'. Продолжить?"))
                {
                    editor.WriteLine("Операция отменена.");
                    return;
                }
            }

            try
            {
                using (document.LockDocument())
                using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                {
                    TinSurface surface;
                    int contourCount = 0;
                    string mode;

                    if (!surfaceId.IsNull)
                    {
                        surface = transaction.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

                        if (surface == null)
                        {
                            editor.WriteError("Выбранный объект — не TIN-поверхность.");
                            return;
                        }

                        mode = "дополнена существующая";
                    }
                    else
                    {
                        ObjectIdCollection contours = SurfaceDefinitionService.CollectLayerCurves(
                            transaction,
                            documentService.ModelSpaceId,
                            ContourLiftService.TargetLayerName);

                        if (contours.Count == 0)
                        {
                            editor.WriteError(
                                $"В слое '{ContourLiftService.TargetLayerName}' нет горизонталей. " +
                                "Сначала выполните CGTINCONTOURS.");
                            return;
                        }

                        surface = SurfaceDefinitionService.CreateFromContours(
                            document.Database, transaction, contours, surfaceName);

                        contourCount = contours.Count;
                        mode = "создана новая";
                    }

                    // Характерные линии
                    int breaklineCount = 0;
                    if (breaklineIds.Length > 0)
                    {
                        var breaklineCollection = new ObjectIdCollection();

                        foreach (ObjectId id in breaklineIds)
                        {
                            var entity = transaction.GetObject(id, OpenMode.ForRead)
                                as Autodesk.AutoCAD.DatabaseServices.Entity;
                            if (entity is Curve)
                            {
                                breaklineCollection.Add(id);
                            }
                        }

                        breaklineCount = SurfaceDefinitionService.AddBreaklines(surface, breaklineCollection);

                        if (breaklineCount < breaklineIds.Length)
                        {
                            editor.WriteLine(
                                $"Из выбранных объектов кривыми являются {breaklineCount} из {breaklineIds.Length}.");
                        }
                    }

                    // Граница участка
                    bool boundaryAdded = false;
                    if (!boundaryId.IsNull)
                    {
                        var boundaryCollection = new ObjectIdCollection();
                        boundaryCollection.Add(boundaryId);

                        SurfaceDefinitionService.AddOuterBoundary(surface, boundaryCollection, true);
                        boundaryAdded = true;
                    }

                    int vertexCount;
                    double minElevation;
                    double maxElevation;

                    SurfaceDefinitionService.RebuildAndMeasure(
                        surface, out vertexCount, out minElevation, out maxElevation);

                    string surfaceDisplayName = surface.Name;
                    transaction.Commit();

                    Report(editor, mode, surfaceDisplayName, contourCount, breaklineCount,
                        boundaryAdded, vertexCount, minElevation, maxElevation, breaklineIds.Length);
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteError("Не удалось построить поверхность: " + ex.Message);
            }
        }

        private static void Report(
            EditorService editor,
            string mode,
            string surfaceName,
            int contourCount,
            int breaklineCount,
            bool boundaryAdded,
            int vertexCount,
            double minElevation,
            double maxElevation,
            int selectedCount)
        {
            editor.WriteLine("--- Сборка поверхности завершена ---");
            editor.WriteLine($"Поверхность '{surfaceName}' ({mode}).");

            if (contourCount > 0)
            {
                editor.WriteLine($"Горизонталей добавлено: {contourCount}");
            }

            editor.WriteLine(breaklineCount > 0
                ? $"Характерных линий (брейклайнов) добавлено: {breaklineCount}"
                : "Характерные линии: не добавлялись.");

            if (selectedCount > 0 && breaklineCount == 0)
            {
                editor.WriteLine("Внимание: среди выбранных объектов не оказалось кривых.");
            }

            editor.WriteLine(boundaryAdded
                ? "Граница: добавлена внешняя с отсечением."
                : "Граница: не задавалась.");

            editor.WriteLine($"Вершин в TIN: {vertexCount}");

            if (vertexCount > 0)
            {
                editor.WriteLine($"Отметки поверхности: {minElevation:F2}…{maxElevation:F2} м");
            }
            else
            {
                editor.WriteLine(
                    "Внимание: поверхность не содержит вершин. Проверьте, что у горизонталей заданы высоты " +
                    "(CGTINCONTOURS) и что граница не отсекает весь участок.");
            }
        }
    }
}
