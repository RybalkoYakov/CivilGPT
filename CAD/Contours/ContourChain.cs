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

        /// <summary>
        /// Упорядоченные вершины всей цепочки. Из них строится одна итоговая
        /// полилиния, а не набор отдельных кусков.
        /// </summary>
        public List<Point3d> Points { get; } = new List<Point3d>();

        /// <summary>Подпись, привязанная к любому из отрезков. Может отсутствовать.</summary>
        public ContourLabel Label { get; set; }

        /// <summary>Восстановленная абсолютная отметка.</summary>
        public double? Elevation { get; set; }

        /// <summary>
        /// Спорная горизонталь: подпись привязана неточно или на линию
        /// претендовало несколько подписей. Такие линии выносятся на отдельный слой.
        /// </summary>
        public bool IsDisputed { get; set; }

        /// <summary>Точка для поиска ближайшей опорной горизонтали.</summary>
        public Point3d Anchor { get; set; }

        /// <summary>
        /// Опорные точки геометрии — для поиска соседних горизонталей.
        /// </summary>
        public List<Point3d> Samples { get; } = new List<Point3d>();

        /// <summary>
        /// Середины разрывов внутри цепочки. Подпись горизонтали стоит как раз
        /// в таком разрыве, поэтому привязка по ним точнее, чем по расстоянию
        /// до ближайшей линии: в плотной группе соседняя линия может быть ближе.
        /// </summary>
        public List<Point3d> GapMidpoints { get; } = new List<Point3d>();

        public bool HasLabel => Label != null;

        public bool IsResolved => Elevation.HasValue;
    }
}
