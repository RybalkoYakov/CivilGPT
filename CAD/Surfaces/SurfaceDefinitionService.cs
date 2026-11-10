using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;

namespace CivilGPT.CAD.Surfaces
{
    /// <summary>
    /// Наполнение TIN-поверхности: горизонтали, характерные линии (бровки, лотки, уступы)
    /// и внешняя граница участка.
    /// </summary>
    public static class SurfaceDefinitionService
    {
        // Параметры добавления определений. Значения подобраны для топосъёмки М 1:500.
        public const double MidOrdinateDistance = 0.1;
        public const double MaximumDistance = 10.0;
        public const double WeedingDistance = 0.5;
        public const double SupplementingDistance = 0.5;

        /// <summary>Угол отсеивания ~5°. Значение задаётся в радианах и безопасно при любой трактовке.</summary>
        public const double WeedingAngle = 0.0872665;

        /// <summary>
        /// Собрать все кривые указанного слоя в пространстве модели.
        /// </summary>
        public static ObjectIdCollection CollectLayerCurves(
            Transaction transaction,
            ObjectId spaceId,
            string layerName)
        {
            var result = new ObjectIdCollection();

            if (transaction == null || string.IsNullOrWhiteSpace(layerName)) return result;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return result;

            foreach (ObjectId id in space)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead)
                    as Autodesk.AutoCAD.DatabaseServices.Entity;
                if (entity == null) continue;

                if (!string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entity is Curve)
                {
                    result.Add(id);
                }
            }

            return result;
        }

        /// <summary>
        /// Создать TIN-поверхность по готовым горизонталям.
        /// </summary>
        public static TinSurface CreateFromContours(
            Database database,
            Transaction transaction,
            ObjectIdCollection contourIds,
            string surfaceName)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            if (contourIds == null || contourIds.Count == 0)
            {
                throw new InvalidOperationException("Нет горизонталей для построения поверхности.");
            }

            ObjectId surfaceId = TinSurface.Create(database, surfaceName);
            var surface = transaction.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

            if (surface == null)
            {
                throw new InvalidOperationException("Не удалось создать TIN-поверхность в чертеже.");
            }

            surface.ContoursDefinition.AddContours(
                contourIds,
                MidOrdinateDistance,
                MaximumDistance,
                WeedingDistance,
                SupplementingDistance);

            return surface;
        }

        /// <summary>
        /// Добавить характерные линии как брейклайны (стандартные).
        /// </summary>
        public static int AddBreaklines(TinSurface surface, ObjectIdCollection breaklineIds)
        {
            if (surface == null || breaklineIds == null || breaklineIds.Count == 0) return 0;

            surface.BreaklinesDefinition.AddStandardBreaklines(
                breaklineIds,
                MidOrdinateDistance,
                MaximumDistance,
                WeedingDistance,
                WeedingAngle);

            return breaklineIds.Count;
        }

        /// <summary>
        /// Добавить внешнюю границу участка с отсечением поверхности по ней.
        /// </summary>
        public static void AddOuterBoundary(
            TinSurface surface,
            ObjectIdCollection boundaryIds,
            bool trim)
        {
            if (surface == null || boundaryIds == null || boundaryIds.Count == 0) return;

            surface.BoundariesDefinition.AddBoundaries(
                boundaryIds,
                MidOrdinateDistance,
                SurfaceBoundaryType.Outer,
                trim);
        }

        /// <summary>
        /// Перестроить поверхность и вернуть количество вершин и диапазон отметок.
        /// </summary>
        public static void RebuildAndMeasure(
            TinSurface surface,
            out int vertexCount,
            out double minElevation,
            out double maxElevation)
        {
            vertexCount = 0;
            minElevation = 0.0;
            maxElevation = 0.0;

            if (surface == null) return;

            surface.Rebuild();

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

            vertexCount = count;

            if (count > 0)
            {
                minElevation = minimum;
                maxElevation = maximum;
            }
        }
    }
}
