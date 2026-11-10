using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Восстановление абсолютных отметок горизонталей.
    /// Полные подписи (205.00, 206) — якоря, сокращённые (60, 70, 90)
    /// разворачиваются от ближайшего якоря: 60 при опоре 204.59 даёт 204.60.
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

            foreach (ContourChain chain in chains)
            {
                if (chain.Label == null || chain.Label.IsFull) continue;

                ContourChain anchor = FindNearest(anchors, chain.Anchor);
                if (anchor == null || !anchor.Elevation.HasValue) continue;

                chain.Elevation = ContourTextParser.ExpandShort(
                    chain.Label.Value, anchor.Elevation.Value);
            }
        }

        private static ContourChain FindNearest(IReadOnlyList<ContourChain> anchors, Point3d position)
        {
            ContourChain best = null;
            double bestDistance = double.MaxValue;

            foreach (ContourChain anchor in anchors)
            {
                double dx = anchor.Anchor.X - position.X;
                double dy = anchor.Anchor.Y - position.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = anchor;
                }
            }

            return best;
        }
    }
}
