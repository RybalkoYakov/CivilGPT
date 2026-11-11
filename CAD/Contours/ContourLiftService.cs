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

        /// <summary>Сколько цельных горизонталей собрано из отрезков.</summary>
        public int ChainsBuilt { get; set; }

        /// <summary>Сколько горизонталей получили подпись.</summary>
        public int ChainsLabeled { get; set; }

        /// <summary>Горизонтали без подписи — высоту взять неоткуда.</summary>
        public int ChainsUnlabeled { get; set; }

        /// <summary>Подписи, которые не удалось развернуть (нет ни одной полной отметки).</summary>
        public int Unresolved { get; set; }

        /// <summary>Случаи, когда на один отрезок претендовали несколько подписей.</summary>
        public int Conflicts { get; set; }

        /// <summary>
        /// Цепочки, внутри которых подписи расходятся по значению.
        /// Признак неверной сшивки: две разные горизонтали склеены в одну.
        /// </summary>
        public int ChainsWithDifferentLabels { get; set; }

        /// <summary>Создано полилиний (по числу отрезков).</summary>
        public int ContoursCreated { get; set; }

        /// <summary>Подробности по цепочкам — для диагностики.</summary>
        public List<ContourChainInfo> Chains { get; } = new List<ContourChainInfo>();

        /// <summary>
        /// Сколько мультивыносок отклонено как инженерные отметки.
        /// Они выглядят как полная отметка (202.34) и, попав в якоря,
        /// сдвигают разворот сокращённых подписей на метр.
        /// </summary>
        public int LabelsIgnoredCallouts { get; set; }

        public double MinElevation { get; set; }

        public double MaxElevation { get; set; }

        public bool HasResult => ContoursCreated > 0;
    }

    /// <summary>
    /// Сведения об одной горизонтали — для диагностики привязки подписей.
    /// </summary>
    public sealed class ContourChainInfo
    {
        public double X { get; set; }

        public double Y { get; set; }

        public string LabelText { get; set; }

        public bool IsFull { get; set; }

        public double? Elevation { get; set; }

        public int Segments { get; set; }
    }

    /// <summary>
    /// Первая команда: превратить линии-горизонтали в полилинии с правильной высотой.
    /// Отрезки одной горизонтали сшиваются, подписи читаются из атрибутов блоков.
    /// Исходные объекты не изменяются — создаются копии.
    /// </summary>
    public static class ContourLiftService
    {
        /// <summary>Слой, в который складываются готовые горизонтали.</summary>
        public const string TargetLayerName = "CIVILGPT_TIN";

        /// <summary>Допуск привязки подписи к линии по умолчанию, м.</summary>
        public const double DefaultLabelTolerance = 1.0;

        /// <summary>Допустимый разрыв между отрезками одной горизонтали, м.</summary>
        public const double DefaultGap = 6.0;

        /// <summary>Допустимое боковое смещение стыкуемых концов, м.</summary>
        public const double DefaultLateralTolerance = 1.0;

        /// <summary>Допустимый излом направления на стыке, градусы.</summary>
        public const double DefaultAngleDegrees = 35.0;

        public static ContourLiftResult Lift(
            Transaction transaction,
            Database database,
            ObjectId modelSpaceId,
            string sourceLayer,
            double labelTolerance,
            double maxGap,
            double lateralTolerance,
            double maxAngleDegrees)
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

            result.LabelsIgnoredCallouts = Collect(
                transaction, modelSpaceId, sourceLayer, curves, labels);

            result.CurvesFound = curves.Count;
            result.LabelsParsed = labels.Count;

            if (curves.Count == 0) return result;

            int conflicts;
            Dictionary<ObjectId, ContourLabel> matched =
                ContourLabelMatcher.Match(curves, labels, labelTolerance, out conflicts);

            result.Conflicts = conflicts;
            result.LabelsMatched = matched.Count;

            List<ContourChain> chains = ContourJoiner.Build(
                curves, maxGap, lateralTolerance, maxAngleDegrees, matched);

            result.ChainsBuilt = chains.Count;

            AssignLabels(chains, matched);

            foreach (ContourChain chain in chains)
            {
                if (chain.HasLabel) result.ChainsLabeled++;
                else result.ChainsUnlabeled++;
            }

            result.ChainsWithDifferentLabels = CountChainsWithDifferentLabels(chains, matched);

            ContourElevationResolver.Resolve(chains);

            foreach (ContourChain chain in chains)
            {
                Point3d position = chain.Anchor;

                if (chain.Label == null)
                {
                    position = GetCentroid(transaction, chain);
                }

                result.Chains.Add(new ContourChainInfo
                {
                    X = position.X,
                    Y = position.Y,
                    LabelText = chain.Label != null ? chain.Label.RawText : null,
                    IsFull = chain.Label != null && chain.Label.IsFull,
                    Elevation = chain.Elevation,
                    Segments = chain.SegmentIds.Count
                });
            }

            ObjectId targetLayerId = EnsureLayer(transaction, database, TargetLayerName, 3);
            var modelSpace = (BlockTableRecord)transaction.GetObject(modelSpaceId, OpenMode.ForWrite);

            foreach (ContourChain chain in chains)
            {
                if (!chain.IsResolved)
                {
                    result.Unresolved++;
                    continue;
                }

                double elevation = chain.Elevation.Value;

                foreach (ObjectId segmentId in chain.SegmentIds)
                {
                    var source = transaction.GetObject(segmentId, OpenMode.ForRead) as Curve;
                    if (source == null) continue;

                    if (!TryCreateLiftedPolyline(
                            transaction, source, elevation, targetLayerId, modelSpace))
                    {
                        continue;
                    }

                    result.ContoursCreated++;

                    if (elevation < result.MinElevation) result.MinElevation = elevation;
                    if (elevation > result.MaxElevation) result.MaxElevation = elevation;
                }
            }

            if (result.ContoursCreated == 0)
            {
                result.MinElevation = 0.0;
                result.MaxElevation = 0.0;
            }

            return result;
        }

        /// <summary>
        /// Центр цепочки — нужен для отчёта о горизонталях без подписи.
        /// </summary>
        private static Point3d GetCentroid(Transaction transaction, ContourChain chain)
        {
            double sumX = 0.0;
            double sumY = 0.0;
            int count = 0;

            foreach (ObjectId segmentId in chain.SegmentIds)
            {
                var curve = transaction.GetObject(segmentId, OpenMode.ForRead, false) as Curve;
                if (curve == null) continue;

                try
                {
                    Point3d point = curve.StartPoint;
                    sumX += point.X;
                    sumY += point.Y;
                    count++;
                }
                catch (System.Exception)
                {
                    // Кривая без геометрии — пропускаем.
                }
            }

            return count == 0 ? Point3d.Origin : new Point3d(sumX / count, sumY / count, 0.0);
        }

        /// <summary>
        /// Раздать подписи цепочкам: полная подпись приоритетнее сокращённой.
        /// </summary>
        private static void AssignLabels(
            IEnumerable<ContourChain> chains,
            IDictionary<ObjectId, ContourLabel> matched)
        {
            foreach (ContourChain chain in chains)
            {
                ContourLabel chosen = null;

                foreach (ObjectId segmentId in chain.SegmentIds)
                {
                    ContourLabel label;
                    if (!matched.TryGetValue(segmentId, out label)) continue;

                    if (chosen == null)
                    {
                        chosen = label;
                        continue;
                    }

                    // Полная отметка информативнее сокращённой.
                    if (label.IsFull && !chosen.IsFull) chosen = label;
                }

                chain.Label = chosen;
                chain.Anchor = chosen != null ? chosen.Position : Point3d.Origin;
            }
        }

        /// <summary>
        /// Сколько цепочек содержит подписи с разными значениями.
        /// Большое число — сигнал, что допуск сшивки слишком велик
        /// и в одну цепочку склеились разные горизонтали.
        /// </summary>
        private static int CountChainsWithDifferentLabels(
            IEnumerable<ContourChain> chains,
            IDictionary<ObjectId, ContourLabel> matched)
        {
            int count = 0;

            foreach (ContourChain chain in chains)
            {
                if (chain.SegmentIds.Count < 2) continue;

                double first = double.NaN;
                bool different = false;

                foreach (ObjectId segmentId in chain.SegmentIds)
                {
                    ContourLabel label;
                    if (!matched.TryGetValue(segmentId, out label)) continue;

                    if (double.IsNaN(first))
                    {
                        first = label.Value;
                        continue;
                    }

                    if (Math.Abs(label.Value - first) > 1e-6)
                    {
                        different = true;
                        break;
                    }
                }

                if (different) count++;
            }

            return count;
        }

        /// <summary>
        /// Собрать горизонтали и подписи с указанного слоя.
        /// Возвращает количество отклонённых мультивыносок.
        /// </summary>
        public static int Collect(
            Transaction transaction,
            ObjectId spaceId,
            string layerName,
            List<Curve> curves,
            List<ContourLabel> labels)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return 0;

            int ignoredCallouts = 0;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return 0;

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

                ContourLabel label = ContourLabelReader.TryRead(transaction, entity);
                if (label == null) continue;

                // Мультивыноска — почти всегда инженерная отметка (дно колодца,
                // лоток, верх покрытия), а не подпись горизонтали: 202.34 или
                // «202.34 д.к.(сущ)». В якоря такие значения пускать нельзя.
                if (label.Source == ContourLabelSource.MLeader)
                {
                    ignoredCallouts++;
                    continue;
                }

                labels.Add(label);
            }

            return ignoredCallouts;
        }

        /// <summary>
        /// Создать копию отрезка в виде полилинии на нужной отметке.
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
