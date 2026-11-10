using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using CivilGPT.Models;

namespace CivilGPT.CAD.Surfaces
{
    /// <summary>
    /// Разбор объектов чертежа на источники данных для TIN-поверхности:
    /// горизонтали (кривые с отметкой) и точечные отметки (тексты, блоки, точки).
    /// </summary>
    public static class ContourCollector
    {
        /// <summary>
        /// Разобрать переданный набор объектов.
        /// </summary>
        public static SurfaceSourceSet Collect(
            Transaction transaction,
            IEnumerable<ObjectId> ids,
            TinSurfaceOptions options)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sources = new SurfaceSourceSet();
            if (ids == null) return sources;

            foreach (ObjectId id in ids)
            {
                Classify(transaction, id, options, sources);
            }

            return sources;
        }

        /// <summary>
        /// Разобрать всё содержимое указанного пространства (обычно — модель).
        /// </summary>
        public static SurfaceSourceSet CollectSpace(
            Transaction transaction,
            ObjectId spaceId,
            TinSurfaceOptions options)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var space = transaction.GetObject(spaceId, OpenMode.ForRead, false) as BlockTableRecord;
            if (space == null)
            {
                return new SurfaceSourceSet();
            }

            var sources = new SurfaceSourceSet();

            foreach (ObjectId id in space)
            {
                Classify(transaction, id, options, sources);
            }

            return sources;
        }

        private static void Classify(
            Transaction transaction,
            ObjectId id,
            TinSurfaceOptions options,
            SurfaceSourceSet sources)
        {
            if (!id.IsValid)
            {
                sources.IgnoredEntities++;
                return;
            }

            var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
            if (entity == null)
            {
                sources.IgnoredEntities++;
                return;
            }

            // 1. Горизонтали — кривые с отметкой
            var curve = entity as Curve;
            if (curve != null)
            {
                if (!options.UseCurvesAsContours)
                {
                    sources.IgnoredEntities++;
                    return;
                }

                double elevation;
                if (!TryGetElevation(curve, out elevation))
                {
                    sources.SkippedNoElevation++;
                    return;
                }

                if (options.WarnOnZeroElevation && Math.Abs(elevation) < 1e-9)
                {
                    sources.ZeroElevationCurves++;
                }

                sources.CurveIds.Add(id);
                return;
            }

            // 2. Текстовые отметки
            if (entity is DBText || entity is MText)
            {
                if (options.UseTextsAsElevationMarks)
                {
                    sources.TextIds.Add(id);
                }
                else
                {
                    sources.IgnoredEntities++;
                }
                return;
            }

            // 3. Блоки-марки
            if (entity is BlockReference)
            {
                if (options.UseBlocksAsElevationMarks)
                {
                    sources.BlockIds.Add(id);
                }
                else
                {
                    sources.IgnoredEntities++;
                }
                return;
            }

            // 4. Точки AutoCAD
            if (entity is DBPoint)
            {
                if (options.UsePointsAsElevationMarks)
                {
                    sources.PointIds.Add(id);
                }
                else
                {
                    sources.IgnoredEntities++;
                }
                return;
            }

            sources.IgnoredEntities++;
        }

        /// <summary>
        /// Отметка кривой. Для LWPOLYLINE берём свойство Elevation,
        /// для остальных кривых — Z начальной точки.
        /// </summary>
        public static bool TryGetElevation(Curve curve, out double elevation)
        {
            elevation = 0.0;
            if (curve == null) return false;

            var polyline = curve as Polyline;
            if (polyline != null)
            {
                elevation = polyline.Elevation;
                return true;
            }

            try
            {
                elevation = curve.StartPoint.Z;
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
