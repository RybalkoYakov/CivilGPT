using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Результат подъёма горизонталей.
    /// </summary>
    public sealed class ContourLiftResult
    {
        public int CurvesFound { get; set; }

        public int LabelsParsed { get; set; }

        public int LabelsMatched { get; set; }

        public int ContoursCreated { get; set; }

        /// <summary>Горизонтали без подписи — высоту взять неоткуда.</summary>
        public int Unlabeled { get; set; }

        /// <summary>Подписи, которые не удалось развернуть (нет ни одной полной отметки).</summary>
        public int Unresolved { get; set; }

        /// <summary>Случаи, когда на одну линию претендовали несколько подписей.</summary>
        public int Conflicts { get; set; }

        public double MinElevation { get; set; }

        public double MaxElevation { get; set; }

        public bool HasResult => ContoursCreated > 0;
    }

    /// <summary>
    /// Первая команда: превратить линии-горизонтали в полилинии
    /// с правильной высотой. Исходные объекты не изменяются — создаются копии.
    /// </summary>
    public static class ContourLiftService
    {
        /// <summary>Слой, в который складываются готовые горизонтали.</summary>
        public const string TargetLayerName = "CIVILGPT_TIN";

        public static ContourLiftResult Lift(
            Transaction transaction,
            Database database,
            ObjectId modelSpaceId,
            string sourceLayer,
            double labelTolerance)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (database == null) throw new ArgumentNullException(nameof(database));

            var result = new ContourLiftResult
            {
                MinElevation = double.MaxValue,
                MaxElevation = double.MinValue
            };

            var curves = new List<Curve>();
            var labels = new List<ContourLabel>();

            Collect(transaction, modelSpaceId, sourceLayer, curves, labels);

            result.CurvesFound = curves.Count;
            result.LabelsParsed = labels.Count;

            if (curves.Count == 0) return result;

            int conflicts;
            List<ContourAssignment> assignments =
                ContourLabelMatcher.Match(curves, labels, labelTolerance, out conflicts);

            result.Conflicts = conflicts;
            result.LabelsMatched = assignments.Count;
            result.Unlabeled = Math.Max(0, curves.Count - assignments.Count);

            ContourElevationResolver.Resolve(assignments);

            ObjectId targetLayerId = EnsureLayer(transaction, database, TargetLayerName, 3);
            var blockTableRecord =
                (BlockTableRecord)transaction.GetObject(modelSpaceId, OpenMode.ForWrite);

            foreach (ContourAssignment assignment in assignments)
            {
                if (!assignment.Elevation.HasValue)
                {
                    result.Unresolved++;
                    continue;
                }

                var source = transaction.GetObject(assignment.CurveId, OpenMode.ForRead) as Curve;
                if (source == null) continue;

                double elevation = assignment.Elevation.Value;

                if (!TryCreateLiftedPolyline(
                        transaction, source, elevation, targetLayerId, blockTableRecord))
                {
                    continue;
                }

                result.ContoursCreated++;

                if (elevation < result.MinElevation) result.MinElevation = elevation;
                if (elevation > result.MaxElevation) result.MaxElevation = elevation;
            }

            if (result.ContoursCreated == 0)
            {
                result.MinElevation = 0.0;
                result.MaxElevation = 0.0;
            }

            return result;
        }

        /// <summary>
        /// Собрать кривые и подписи с указанного слоя.
        /// </summary>
        public static void Collect(
            Transaction transaction,
            ObjectId spaceId,
            string layerName,
            List<Curve> curves,
            List<ContourLabel> labels)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return;

            foreach (ObjectId id in space)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                if (!string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var curve = entity as Curve;
                if (curve != null)
                {
                    curves.Add(curve);
                    continue;
                }

                ContourLabel label = ReadLabel(entity);
                if (label != null) labels.Add(label);
            }
        }

        private static ContourLabel ReadLabel(Entity entity)
        {
            string rawText = null;
            Point3d position = Point3d.Origin;
            double height = 0.0;

            var text = entity as DBText;
            if (text != null)
            {
                rawText = text.TextString;
                position = text.Position;
                height = text.Height;
            }
            else
            {
                var mText = entity as MText;
                if (mText == null) return null;

                rawText = mText.Text;
                position = mText.Location;
                height = mText.TextHeight;
            }

            double value;
            bool isFull;
            if (!ContourTextParser.TryParse(rawText, out value, out isFull)) return null;

            return new ContourLabel
            {
                TextId = entity.ObjectId,
                Position = position,
                RawText = rawText,
                Value = value,
                IsFull = isFull,
                Height = height
            };
        }

        /// <summary>
        /// Создать копию линии в виде полилинии на нужной отметке.
        /// </summary>
        private static bool TryCreateLiftedPolyline(
            Transaction transaction,
            Curve source,
            double elevation,
            ObjectId layerId,
            BlockTableRecord blockTableRecord)
        {
            try
            {
                Curve3d geometry = source.GetGeCurve();

                // Вектор нормали ZAxis: горизонталь всегда лежит в горизонтальной плоскости.
                Curve copy = Curve.CreateFromGeCurve(geometry, Vector3d.ZAxis);
                if (copy == null) return false;

                var polyline = copy as Polyline;
                if (polyline != null)
                {
                    polyline.Elevation = elevation;
                }
                else
                {
                    double delta = elevation - copy.StartPoint.Z;
                    copy.TransformBy(Matrix3d.Displacement(new Vector3d(0.0, 0.0, delta)));
                }

                copy.LayerId = layerId;
                copy.ColorIndex = 256; // ByLayer

                blockTableRecord.AppendEntity(copy);
                transaction.AddNewlyCreatedDBObject(copy, true);

                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Получить слой или создать его.
        /// </summary>
        public static ObjectId EnsureLayer(
            Transaction transaction,
            Database database,
            string layerName,
            short colorIndex)
        {
            var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);

            if (layerTable.Has(layerName))
            {
                return layerTable[layerName];
            }

            layerTable.UpgradeOpen();

            var record = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };

            ObjectId layerId = layerTable.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            return layerId;
        }
    }
}
