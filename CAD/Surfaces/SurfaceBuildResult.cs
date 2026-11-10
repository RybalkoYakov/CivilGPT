using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Surfaces
{
    /// <summary>
    /// Результат построения TIN-поверхности.
    /// </summary>
    public class SurfaceBuildResult
    {
        public ObjectId SurfaceId { get; set; }

        public string SurfaceName { get; set; }

        /// <summary>Сколько кривых добавлено как горизонтали.</summary>
        public int ContourCount { get; set; }

        public int TextMarkCount { get; set; }

        public int BlockMarkCount { get; set; }

        public int PointMarkCount { get; set; }

        /// <summary>Количество вершин TIN после перестроения.</summary>
        public int VertexCount { get; set; }

        public double MinimumElevation { get; set; }

        public double MaximumElevation { get; set; }

        public int ZeroElevationCurves { get; set; }

        public int IgnoredEntities { get; set; }

        public override string ToString()
        {
            return $"Поверхность '{SurfaceName}': горизонталей {ContourCount}, " +
                   $"отметок {TextMarkCount + BlockMarkCount + PointMarkCount}, " +
                   $"вершин {VertexCount}, отметки {MinimumElevation:F2}…{MaximumElevation:F2} м.";
        }
    }
}
