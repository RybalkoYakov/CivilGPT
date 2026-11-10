using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Восстановление абсолютных отметок по подписям.
    /// Полные отметки задают «якоря», сокращённые разворачиваются от ближайшего якоря.
    /// </summary>
    public static class ContourElevationResolver
    {
        public static void Resolve(IList<ContourAssignment> assignments)
        {
            if (assignments == null || assignments.Count == 0) return;

            var anchors = new List<ContourAssignment>();

            foreach (ContourAssignment assignment in assignments)
            {
                if (!assignment.IsFull) continue;

                assignment.Elevation = assignment.RawValue;
                anchors.Add(assignment);
            }

            if (anchors.Count == 0)
            {
                // Ни одной полной отметки — разворачивать сокращённые не от чего.
                return;
            }

            foreach (ContourAssignment assignment in assignments)
            {
                if (assignment.IsFull) continue;

                ContourAssignment anchor = FindNearest(anchors, assignment.Position);
                if (anchor == null || !anchor.Elevation.HasValue) continue;

                assignment.Elevation = ContourTextParser.ExpandShort(
                    assignment.RawValue, anchor.Elevation.Value);
            }
        }

        private static ContourAssignment FindNearest(
            IReadOnlyList<ContourAssignment> anchors,
            Point3d position)
        {
            ContourAssignment best = null;
            double bestDistance = double.MaxValue;

            foreach (ContourAssignment anchor in anchors)
            {
                double dx = anchor.Position.X - position.X;
                double dy = anchor.Position.Y - position.Y;
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
