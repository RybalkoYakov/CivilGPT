using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Восстановление абсолютных отметок горизонталей.
    ///
    /// Полные подписи (205.00, 203) — якоря. Сокращённая подпись (60, 90) задаёт
    /// только две последние цифры, поэтому её значение зависит от того, между
    /// какими якорями лежит линия. Простое «ближайшее к якорю» значение даёт
    /// ошибку в метр на границе метра: якорь 203.00 и подпись 60 дают 202.60,
    /// хотя рядом стоит 203.50 и правильное значение — 203.60.
    ///
    /// Поэтому кандидат выбирается по «полосе» между двумя соседними якорями:
    /// чем уже полоса и чем ближе её якоря к линии, тем правдоподобнее значение.
    /// </summary>
    public static class ContourElevationResolver
    {
        public static void Resolve(IList<ContourChain> chains)
        {
            if (chains == null || chains.Count == 0) return;

            var anchors = new List<ContourChain>();

            foreach (ContourChain chain in chains)
            {
                if (chain.Label == null || !chain.Label.IsFull) continue;

                chain.Elevation = chain.Label.Value;
                anchors.Add(chain);
            }

            if (anchors.Count == 0) return;

            List<double> anchorValues = GetSortedValues(anchors);

            foreach (ContourChain chain in chains)
            {
                if (chain.Label == null || chain.Label.IsFull) continue;

                double? elevation = ChooseByBand(
                    chain.Label.Value, chain.Anchor, anchorValues, anchors);

                if (!elevation.HasValue)
                {
                    elevation = ChooseNearestToAnchor(
                        chain.Label.Value, chain.Anchor, anchors);
                }

                chain.Elevation = elevation;
            }
        }

        private static List<double> GetSortedValues(IReadOnlyList<ContourChain> anchors)
        {
            var values = new List<double>();

            foreach (ContourChain anchor in anchors)
            {
                if (!anchor.Elevation.HasValue) continue;

                double value = anchor.Elevation.Value;

                if (values.Count == 0 || Math.Abs(values[values.Count - 1] - value) > 1e-6)
                {
                    values.Add(value);
                }
            }

            values.Sort();
            return values;
        }

        /// <summary>
        /// Выбрать значение по полосе между соседними якорями.
        /// </summary>
        private static double? ChooseByBand(
            double shortValue,
            Point3d position,
            IReadOnlyList<double> anchorValues,
            IReadOnlyList<ContourChain> anchors)
        {
            if (anchorValues.Count < 2) return null;

            double fraction = shortValue / 100.0;

            double minimum = anchorValues[0];
            double maximum = anchorValues[anchorValues.Count - 1];

            double? best = null;
            double bestScore = double.MaxValue;

            int firstMeter = (int)Math.Floor(minimum) - 1;
            int lastMeter = (int)Math.Ceiling(maximum) + 1;

            for (int meter = firstMeter; meter <= lastMeter; meter++)
            {
                double candidate = meter + fraction;

                if (candidate < minimum - 1e-9 || candidate > maximum + 1e-9) continue;
                if (candidate <= minimum + 1e-9 || candidate >= maximum - 1e-9) continue;

                int index = FindBand(anchorValues, candidate);
                if (index < 0) continue;

                double lower = anchorValues[index];
                double upper = anchorValues[index + 1];
                double width = upper - lower;

                double distance = DistanceToBand(anchors, lower, upper, position);

                // Ширина полосы — вес 5, расстояние до её якорей — основной вес.
                double score = distance + width * 5.0;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static int FindBand(IReadOnlyList<double> values, double candidate)
        {
            for (int i = 0; i < values.Count - 1; i++)
            {
                if (candidate > values[i] + 1e-9 && candidate < values[i + 1] - 1e-9)
                {
                    return i;
                }
            }

            return -1;
        }

        private static double DistanceToBand(
            IReadOnlyList<ContourChain> anchors,
            double lower,
            double upper,
            Point3d position)
        {
            double best = double.MaxValue;

            foreach (ContourChain anchor in anchors)
            {
                if (!anchor.Elevation.HasValue) continue;

                double value = anchor.Elevation.Value;
                if (Math.Abs(value - lower) > 1e-6 && Math.Abs(value - upper) > 1e-6) continue;

                double dx = anchor.Anchor.X - position.X;
                double dy = anchor.Anchor.Y - position.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < best) best = distance;
            }

            return best;
        }

        /// <summary>
        /// Резервный вариант: ближайшее значение к ближайшему якорю.
        /// </summary>
        private static double? ChooseNearestToAnchor(
            double shortValue,
            Point3d position,
            IReadOnlyList<ContourChain> anchors)
        {
            ContourChain nearest = null;
            double bestDistance = double.MaxValue;

            foreach (ContourChain anchor in anchors)
            {
                double dx = anchor.Anchor.X - position.X;
                double dy = anchor.Anchor.Y - position.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = anchor;
                }
            }

            if (nearest == null || !nearest.Elevation.HasValue) return null;

            return ContourTextParser.ExpandShort(shortValue, nearest.Elevation.Value);
        }
    }
}
