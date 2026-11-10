using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Одна горизонталь. Может состоять из нескольких полилиний:
    /// линия рвётся под подписью, на пересечении с дорогой, у здания.
    /// </summary>
    public sealed class ContourChain
    {
        public ContourChain()
        {
            SegmentIds = new List<ObjectId>();
        }

        /// <summary>Отрезки, из которых состоит горизонталь.</summary>
        public List<ObjectId> SegmentIds { get; private set; }

        /// <summary>Подпись, привязанная к любому из отрезков. Может отсутствовать.</summary>
        public ContourLabel Label { get; set; }

        /// <summary>Восстановленная абсолютная отметка.</summary>
        public double? Elevation { get; set; }

        /// <summary>Точка для поиска ближайшей опорной горизонтали.</summary>
        public Point3d Anchor { get; set; }

        public bool HasLabel => Label != null;

        public bool IsResolved => Elevation.HasValue;
    }
}
