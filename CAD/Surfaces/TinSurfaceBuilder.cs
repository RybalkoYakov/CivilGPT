using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using CivilGPT.Models;

namespace CivilGPT.CAD.Surfaces
{
    /// <summary>
    /// Построение TIN-поверхности Civil 3D по горизонталям и иным отметкам.
    /// Последовательность: создать поверхность → добавить определения →
    /// перестроить → собрать статистику по вершинам.
    /// </summary>
    public static class TinSurfaceBuilder
    {
        public static SurfaceBuildResult Build(
            Database database,
            Transaction transaction,
            SurfaceSourceSet sources,
            TinSurfaceOptions options)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (options == null) throw new ArgumentNullException(nameof(options));

            string error;
            if (!options.IsValid(out error))
            {
                throw new InvalidOperationException(error);
            }

            if (sources.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Не найдено ни одного объекта с отметками: горизонталей, текстов-отметок, блоков или точек.");
            }

            // 1. Создаём поверхность
            ObjectId surfaceId = TinSurface.Create(database, options.SurfaceName);
            var surface = transaction.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

            if (surface == null)
            {
                throw new InvalidOperationException("Не удалось создать TIN-поверхность в чертеже.");
            }

            var result = new SurfaceBuildResult
            {
                SurfaceId = surfaceId,
                SurfaceName = options.SurfaceName,
                ZeroElevationCurves = sources.ZeroElevationCurves,
                IgnoredEntities = sources.IgnoredEntities
            };

            // 2. Горизонтали
            if (sources.CurveIds.Count > 0)
            {
                surface.ContoursDefinition.AddContours(
                    sources.CurveIds,
                    options.MidOrdinateDistance,
                    options.MaximumDistance,
                    options.WeedingDistance,
                    options.SupplementingDistance);

                result.ContourCount = sources.CurveIds.Count;
            }

            // 3. Точечные отметки: тексты, блоки, точки
            if (sources.TextIds.Count > 0)
            {
                surface.DrawingObjectsDefinition.AddFromTexts(sources.TextIds, "Отметки (текст)");
                result.TextMarkCount = sources.TextIds.Count;
            }

            if (sources.BlockIds.Count > 0)
            {
                surface.DrawingObjectsDefinition.AddFromBlocks(sources.BlockIds, "Отметки (блоки)");
                result.BlockMarkCount = sources.BlockIds.Count;
            }

            if (sources.PointIds.Count > 0)
            {
                surface.DrawingObjectsDefinition.AddFromPoints(sources.PointIds, "Отметки (точки)");
                result.PointMarkCount = sources.PointIds.Count;
            }

            // 4. Перестроение
            surface.Rebuild();

            // 5. Статистика по вершинам
            CollectStatistics(surface, result);

            return result;
        }

        private static void CollectStatistics(TinSurface surface, SurfaceBuildResult result)
        {
            int count = 0;
            double minimum = double.MaxValue;
            double maximum = double.MinValue;

            try
            {
                foreach (TinSurfaceVertex vertex in surface.Vertices)
                {
                    double elevation = vertex.Location.Z;

                    if (elevation < minimum) minimum = elevation;
                    if (elevation > maximum) maximum = elevation;

                    count++;
                }
            }
            catch (System.Exception)
            {
                return;
            }

            result.VertexCount = count;

            if (count > 0)
            {
                result.MinimumElevation = minimum;
                result.MaximumElevation = maximum;
            }
        }
    }
}
