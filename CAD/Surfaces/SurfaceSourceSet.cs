using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Surfaces
{
    /// <summary>
    /// Набор объектов чертежа, пригодных для построения TIN-поверхности:
    /// кривые-горизонтали и точечные отметки (тексты, блоки, точки).
    /// </summary>
    public sealed class SurfaceSourceSet
    {
        public SurfaceSourceSet()
        {
            CurveIds = new ObjectIdCollection();
            TextIds = new ObjectIdCollection();
            BlockIds = new ObjectIdCollection();
            PointIds = new ObjectIdCollection();
        }

        /// <summary>Кривые с ненулевой отметкой — горизонтали.</summary>
        public ObjectIdCollection CurveIds { get; }

        /// <summary>Тексты и мультитексты с отметками.</summary>
        public ObjectIdCollection TextIds { get; }

        /// <summary>Вхождения блоков с отметками (марки, реперы).</summary>
        public ObjectIdCollection BlockIds { get; }

        /// <summary>Точки AutoCAD с ненулевой отметкой.</summary>
        public ObjectIdCollection PointIds { get; }

        /// <summary>Кривые с нулевой отметкой (подозрительные горизонтали).</summary>
        public int ZeroElevationCurves { get; set; }

        /// <summary>Кривые, у которых отметку получить не удалось.</summary>
        public int SkippedNoElevation { get; set; }

        /// <summary>Объекты, не подходящие для поверхности.</summary>
        public int IgnoredEntities { get; set; }

        public int TotalSources =>
            CurveIds.Count + TextIds.Count + BlockIds.Count + PointIds.Count;

        public bool IsEmpty => TotalSources == 0;
    }
}
