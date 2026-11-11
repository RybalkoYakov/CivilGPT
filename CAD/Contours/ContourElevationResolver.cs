using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Восстановление абсолютных отметок горизонталей.
    ///
    /// Полные подписи (203, 205.00) — опорные. Сокращённая подпись (60, 90) задаёт
    /// только две последние цифры, поэтому «ближайшее к опоре» значение ошибается
    /// на метр на границе метра: опора 203.00 и подпись 60 дают 202.60,
    /// хотя правильное значение — 203.60.
    ///
    /// Поэтому отметки распространяются по соседству горизонталей: от опорной
    /// линии к соседней ровно на одно сечение, а последние две цифры подписи
    /// проверяют, вверх идти или вниз. Так ряд 203.10, 203.20, … 203.60
    /// выстраивается от опоры без промаха на метр.
    /// </summary>
    public static class ContourElevationResolver
    {
        /// <summary>Расстояние, при котором горизонтали считаются соседними, м.</summary>
        private const double NeighborDistance = 2.0;

        /// <summary>Допуск проверки последних цифр подписи.</summary>
        private const double RemainderTolerance = 1e-3;

        public static void Resolve(IList<ContourChain> chains, double step)
        {
            if (chains == null || chains.Count == 0) return;
            if (step <= 0) step = 0.1;

            var anchors = new List<ContourChain>();

            foreach (ContourChain chain in chains)
            {
                if (chain.Label == null || !chain.Label.IsFull) continue;

                chain.Elevation = chain.Label.Value;
                anchors.Add(chain);
            }

            if (anchors.Count == 0) return;

            double minimum = double.MaxValue;
            double maximum = double.MinValue;

            foreach (ContourChain anchor in anchors)
            {
                if (!anchor.Elevation.HasValue) continue;

                if (anchor.Elevation.Value < minimum) minimum = anchor.Elevation.Value;
                if (anchor.Elevation.Value > maximum) maximum = anchor.Elevation.Value;
            }

            PropagateByNeighbours(chains, step, minimum, maximum);

            // Что не удалось определить по соседям — по ближайшей опоре,
            // но с той же проверкой допустимого диапазона.
            foreach (ContourChain chain in chains)
            {
                if (chain.Elevation.HasValue) continue;
                if (chain.Label == null || chain.Label.IsFull) continue;

                chain.Elevation = ChooseNearestToAnchor(
                    chain.Label.Value, chain.Anchor, anchors, step, minimum, maximum);
            }
        }

        /// <summary>
        /// Значение должно лежать между крайними опорными горизонталями.
        /// Запаса не даём: при опорах 203.00…204.00 значение 202.90 невозможно —
        /// иначе соседняя опора 203.00 тянет подпись «90» на метр вниз.
        /// </summary>
        private static bool IsAllowed(double value, double step, double minimum, double maximum)
        {
            return value >= minimum - 1e-6 && value <= maximum + 1e-6;
        }

        private static void PropagateByNeighbours(
            IList<ContourChain> chains,
            double step,
            double minimum,
            double maximum)
        {
            List<int>[] neighbors = BuildNeighbours(chains);

            bool changed = true;
            int guard = 0;

            while (changed && guard++ < 500)
            {
                changed = false;

                for (int i = 0; i < chains.Count; i++)
                {
                    ContourChain chain = chains[i];

                    if (chain.Elevation.HasValue) continue;
                    if (chain.Label == null || chain.Label.IsFull) continue;

                    var candidates = new List<double>(4);

                    foreach (int index in neighbors[i])
                    {
                        ContourChain neighbor = chains[index];
                        if (!neighbor.Elevation.HasValue) continue;

                        AddCandidate(candidates, neighbor.Elevation.Value + step, chain.Label.Value, step, minimum, maximum);
                        AddCandidate(candidates, neighbor.Elevation.Value - step, chain.Label.Value, step, minimum, maximum);
                    }

                    // Значение принимается, только если оно единственное:
                    // иначе соседи неоднозначны и лучше оставить неизвестным.
                    if (candidates.Count == 1)
                    {
                        chain.Elevation = candidates[0];
                        changed = true;
                    }
                }
            }
        }

        private static void AddCandidate(
            List<double> candidates,
            double value,
            double shortLabel,
            double step,
            double minimum,
            double maximum)
        {
            if (!MatchesRemainder(value, shortLabel)) return;
            if (!IsAllowed(value, step, minimum, maximum)) return;

            foreach (double existing in candidates)
            {
                if (Math.Abs(existing - value) < 1e-6) return;
            }

            candidates.Add(value);
        }

        /// <summary>
        /// Совпадают ли последние две цифры значения с подписью
        /// (90 → …90, 5 → …05).
        /// </summary>
        private static bool MatchesRemainder(double value, double shortLabel)
        {
            double scaled = value * 100.0;

            if (Math.Abs(scaled - Math.Round(scaled)) > RemainderTolerance * 100.0) return false;

            int remainder = (int)Math.Round(scaled) % 100;
            if (remainder < 0) remainder += 100;

            return remainder == (int)Math.Round(shortLabel);
        }

        private static List<int>[] BuildNeighbours(IList<ContourChain> chains)
        {
            int count = chains.Count;
            var neighbors = new List<int>[count];
            var boxes = new double[count][];

            for (int i = 0; i < count; i++)
            {
                neighbors[i] = new List<int>();
                boxes[i] = GetBox(chains[i]);
            }

            for (int i = 0; i < count; i++)
            {
                if (boxes[i] == null) continue;

                for (int j = i + 1; j < count; j++)
                {
                    if (boxes[j] == null) continue;
                    if (BoxDistance(boxes[i], boxes[j]) > NeighborDistance) continue;

                    double distance = MinDistance(chains[i].Samples, chains[j].Samples);
                    if (distance > NeighborDistance) continue;

                    neighbors[i].Add(j);
                    neighbors[j].Add(i);
                }
            }

            return neighbors;
        }

        private static double[] GetBox(ContourChain chain)
        {
            if (chain.Samples.Count == 0) return null;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (Point3d point in chain.Samples)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            return new[] { minX, minY, maxX, maxY };
        }

        private static double BoxDistance(double[] a, double[] b)
        {
            double dx = Math.Max(0.0, Math.Max(a[0] - b[2], b[0] - a[2]));
            double dy = Math.Max(0.0, Math.Max(a[1] - b[3], b[1] - a[3]));

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double MinDistance(IReadOnlyList<Point3d> first, IReadOnlyList<Point3d> second)
        {
            double best = double.MaxValue;

            foreach (Point3d a in first)
            {
                foreach (Point3d b in second)
                {
                    double dx = a.X - b.X;
                    double dy = a.Y - b.Y;
                    double distance = dx * dx + dy * dy;

                    if (distance < best) best = distance;
                }
            }

            return best == double.MaxValue ? double.MaxValue : Math.Sqrt(best);
        }

        /// <summary>
        /// Резервный вариант: среди допустимых значений с такими же последними
        /// цифрами выбрать ближайшее к ближайшей опоре.
        /// </summary>
        private static double? ChooseNearestToAnchor(
            double shortValue,
            Point3d position,
            IReadOnlyList<ContourChain> anchors,
            double step,
            double minimum,
            double maximum)
        {
            ContourChain nearest = null;
            double bestDistance = double.MaxValue;

            foreach (ContourChain anchor in anchors)
            {
                double dx = anchor.Anchor.X - position.X;
                double dy = anchor.Anchor.Y - position.Y;
                double distance = dx * dx + dy * dy;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = anchor;
                }
            }

            if (nearest == null || !nearest.Elevation.HasValue) return null;

            double fraction = shortValue / 100.0;
            double reference = nearest.Elevation.Value;

            double? best = null;
            double bestDelta = double.MaxValue;

            int firstMeter = (int)Math.Floor(reference) - 2;
            int lastMeter = (int)Math.Ceiling(reference) + 2;

            for (int meter = firstMeter; meter <= lastMeter; meter++)
            {
                double candidate = meter + fraction;

                if (!IsAllowed(candidate, step, minimum, maximum)) continue;

                double delta = Math.Abs(candidate - reference);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
