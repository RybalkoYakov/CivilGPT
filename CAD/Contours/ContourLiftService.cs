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

        /// <summary>Подписей взято с других слоёв.</summary>
        public int LabelsFromOtherLayers { get; set; }

        /// <summary>Спорных горизонталей отрисовано (вынесены на отдельный слой).</summary>
        public int DisputedCreated { get; set; }

        /// <summary>Объекты-носители подписей — чтобы можно было вынести их на свой слой.</summary>
        public List<ObjectId> LabelSourceIds { get; } = new List<ObjectId>();

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

        /// <summary>Привязка подписи спорная.</summary>
        public bool IsDisputed { get; set; }
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

        /// <summary>Слой для горизонталей со спорной привязкой подписи.</summary>
        public const string DisputedLayerName = "CIVILGPT_TIN_СПОРНЫЕ";

        /// <summary>Слой, на который можно вынести подписи горизонталей.</summary>
        public const string LabelLayerName = "CIVILGPT_МЕТКИ";

        /// <summary>Допуск привязки подписи к линии по умолчанию, м.</summary>
        public const double DefaultLabelTolerance = 1.0;

        /// <summary>Допустимый разрыв между отрезками одной горизонтали, м.</summary>
        public const double DefaultGap = 6.0;

        /// <summary>Допустимое боковое смещение стыкуемых концов, м.</summary>
        public const double DefaultLateralTolerance = 1.0;

        /// <summary>Допустимый излом направления на стыке, градусы.</summary>
        public const double DefaultAngleDegrees = 35.0;

        /// <summary>Сечение рельефа по умолчанию, м.</summary>
        public const double DefaultStep = 0.1;

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

            int foreignLabels;
            result.LabelsIgnoredCallouts = Collect(
                transaction, modelSpaceId, sourceLayer, curves, labels, out foreignLabels);

            result.LabelsFromOtherLayers = foreignLabels;

            result.CurvesFound = curves.Count;
            result.LabelsParsed = labels.Count;

            if (curves.Count == 0) return result;

            int conflicts;
            HashSet<ObjectId> uncertain;
            Dictionary<ObjectId, ContourLabel> matched =
                ContourLabelMatcher.Match(curves, labels, labelTolerance, out conflicts, out uncertain);

            result.Conflicts = conflicts;
            result.LabelsMatched = matched.Count;

            List<ContourChain> chains = ContourJoiner.Build(curves, maxGap);

            result.ChainsBuilt = chains.Count;

            foreach (ContourChain chain in chains)
            {
                CollectSamples(transaction, chain);
            }

            AssignLabels(chains, matched);

            foreach (ContourChain chain in chains)
            {
                if (chain.HasLabel) result.ChainsLabeled++;
                else result.ChainsUnlabeled++;
            }

            result.ChainsWithDifferentLabels = CountChainsWithDifferentLabels(chains, matched);

            // Спорной считается цепочка, у которой подпись привязана неточно
            // или на её отрезки претендовало несколько подписей.
            foreach (ContourChain chain in chains)
            {
                foreach (ObjectId segmentId in chain.SegmentIds)
                {
                    if (uncertain.Contains(segmentId))
                    {
                        chain.IsDisputed = true;
                        break;
                    }
                }
            }

            foreach (ContourLabel label in labels)
            {
                if (!label.SourceId.IsNull)
                {
                    result.LabelSourceIds.Add(label.SourceId);
                }
            }

            ContourElevationResolver.Resolve(chains, DefaultStep);

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
                    Segments = chain.SegmentIds.Count,
                    IsDisputed = chain.IsDisputed
                });
            }

            ObjectId targetLayerId = EnsureLayer(transaction, database, TargetLayerName, 3);
            ObjectId disputedLayerId = EnsureLayer(transaction, database, DisputedLayerName, 1);
            var modelSpace = (BlockTableRecord)transaction.GetObject(modelSpaceId, OpenMode.ForWrite);

            foreach (ContourChain chain in chains)
            {
                if (!chain.IsResolved)
                {
                    result.Unresolved++;
                    continue;
                }

                double elevation = chain.Elevation.Value;
                ObjectId chainLayerId = chain.IsDisputed ? disputedLayerId : targetLayerId;

                foreach (ObjectId segmentId in chain.SegmentIds)
                {
                    var source = transaction.GetObject(segmentId, OpenMode.ForRead) as Curve;
                    if (source == null) continue;

                    if (!TryCreateLiftedPolyline(
                            transaction, source, elevation, chainLayerId, modelSpace))
                    {
                        continue;
                    }

                    result.ContoursCreated++;

                    if (chain.IsDisputed) result.DisputedCreated++;

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
        /// Опорные точки цепочки — по ним ищутся соседние горизонтали.
        /// </summary>
        private static void CollectSamples(Transaction transaction, ContourChain chain)
        {
            foreach (ObjectId segmentId in chain.SegmentIds)
            {
                var curve = transaction.GetObject(segmentId, OpenMode.ForRead, false) as Curve;
                if (curve == null) continue;

                try
                {
                    chain.Samples.Add(curve.StartPoint);
                    chain.Samples.Add(curve.EndPoint);

                    double middle = (curve.StartParam + curve.EndParam) * 0.5;
                    chain.Samples.Add(curve.GetPointAtParameter(middle));
                }
                catch (System.Exception)
                {
                    // Кривая без геометрии — пропускаем.
                }
            }
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
        /// Привязать подписи по разрывам цепочек. Подпись горизонтали стоит
        /// именно в разрыве своей линии, поэтому такой признак надёжнее
        /// расстояния до ближайшей кривой: в плотной группе соседняя
        /// горизонталь может оказаться ближе и дать сдвиг на одно сечение.
        /// </summary>
        private static Dictionary<ContourChain, ContourLabel> MatchLabelsByGaps(
            IList<ContourChain> chains,
            IReadOnlyList<ContourLabel> labels)
        {
            var result = new Dictionary<ContourChain, ContourLabel>();
            var best = new Dictionary<ContourChain, double>();

            const double gapTolerance = 1.5;

            foreach (ContourLabel label in labels)
            {
                ContourChain bestChain = null;
                double bestDistance = double.MaxValue;

                foreach (ContourChain chain in chains)
                {
                    foreach (Point3d gap in chain.GapMidpoints)
                    {
                        double dx = gap.X - label.Position.X;
                        double dy = gap.Y - label.Position.Y;
                        double distance = dx * dx + dy * dy;

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestChain = chain;
                        }
                    }
                }

                if (bestChain == null) continue;
                if (bestDistance > gapTolerance * gapTolerance) continue;

                ContourLabel existing;
                if (result.TryGetValue(bestChain, out existing))
                {
                    if (existing.IsFull && !label.IsFull) continue;
                    if (existing.IsFull == label.IsFull && bestDistance >= best[bestChain]) continue;
                }

                result[bestChain] = label;
                best[bestChain] = bestDistance;
            }

            return result;
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
        /// Собрать горизонтали с указанного слоя, а подписи — со всех слоёв:
        /// на разных листах подписи лежат то вместе с линиями, то отдельным слоем.
        /// Слои с таблицами (экспликации, стройсетка) исключаются: там номера
        /// зданий вида 106.12, которые выглядят как отметки.
        /// </summary>
        public static int Collect(
            Transaction transaction,
            ObjectId spaceId,
            string layerName,
            List<Curve> curves,
            List<ContourLabel> labels,
            out int foreignLabels)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                foreignLabels = 0;
                return 0;
            }

            int ignoredCallouts = 0;
            foreignLabels = 0;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return 0;

            var ownLabels = new List<ContourLabel>();
            var otherLayerLabels = new List<ContourLabel>();

            foreach (ObjectId id in space)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                string entityLayer = entity.Layer ?? string.Empty;

                bool onSourceLayer = string.Equals(
                    entityLayer, layerName, StringComparison.OrdinalIgnoreCase);

                var curve = entity as Curve;

                if (curve != null)
                {
                    if (onSourceLayer) curves.Add(curve);
                    continue;
                }

                if (!onSourceLayer) continue;

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

                if (onSourceLayer) ownLabels.Add(label);
                else otherLayerLabels.Add(label);
            }

            labels.AddRange(ownLabels);

            // Подписи берутся только со слоя горизонталей: на чужих слоях лежат
            // подписи других листов, и они дают ложные опоры (202.5 рядом с 203.4).
            foreignLabels = 0;

            return ignoredCallouts;
        }

        /// <summary>
        /// Подписи с других слоёв принимаются только в диапазоне отметок
        /// самого слоя горизонталей: иначе на чертеже находится «1.10»
        /// или номер здания и становится опорой.
        /// </summary>
        private static IEnumerable<ContourLabel> FilterForeign(
            IReadOnlyList<ContourLabel> ownLabels,
            IReadOnlyList<ContourLabel> foreignLabels)
        {
            double minimum = double.MaxValue;
            double maximum = double.MinValue;

            foreach (ContourLabel label in ownLabels)
            {
                if (!label.IsFull) continue;

                if (label.Value < minimum) minimum = label.Value;
                if (label.Value > maximum) maximum = label.Value;
            }

            bool hasBand = maximum > minimum;

            foreach (ContourLabel label in foreignLabels)
            {
                if (!label.IsFull)
                {
                    // Сокращённые подписи проверяются диапазоном позже, при развороте.
                    yield return label;
                    continue;
                }

                if (hasBand && (label.Value < minimum - 5.0 || label.Value > maximum + 5.0))
                {
                    continue;
                }

                yield return label;
            }
        }

        /// <summary>
        /// Слои оформления, где числа — это номера зданий, а не отметки.
        /// </summary>
        private static bool IsTableLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;

            return layerName.IndexOf("экспликац", StringComparison.OrdinalIgnoreCase) >= 0
                   || layerName.IndexOf("стройсетк", StringComparison.OrdinalIgnoreCase) >= 0;
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
        /// Сколько объектов лежит на слое в пространстве модели.
        /// Нужно, чтобы предупредить о результате предыдущего запуска.
        /// </summary>
        public static int CountLayerEntities(
            Transaction transaction,
            ObjectId spaceId,
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return 0;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return 0;

            int count = 0;

            foreach (ObjectId id in space)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (entity == null) continue;

                if (string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Удалить все объекты слоя (результат предыдущего запуска).
        /// </summary>
        public static int EraseLayerEntities(
            Transaction transaction,
            ObjectId spaceId,
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return 0;

            var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
            if (space == null) return 0;

            var toErase = new List<ObjectId>();

            foreach (ObjectId id in space)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (entity == null) continue;

                if (string.Equals(entity.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    toErase.Add(id);
                }
            }

            foreach (ObjectId id in toErase)
            {
                var entity = transaction.GetObject(id, OpenMode.ForWrite) as Entity;
                entity?.Erase();
            }

            return toErase.Count;
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
