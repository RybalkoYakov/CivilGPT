namespace CivilGPT.Models
{
    /// <summary>
    /// Параметры случайного горизонтального отклонения точек COGO в осях текущей ПСК.
    /// </summary>
    public class HorizontalOffsetOptions
    {
        public double MinOffsetX { get; set; } = -0.005;

        public double MaxOffsetX { get; set; } = 0.005;

        public double MinOffsetY { get; set; } = -0.005;

        public double MaxOffsetY { get; set; } = 0.005;

        public bool IsValid(out string error)
        {
            if (MinOffsetX > MaxOffsetX)
            {
                error = $"Мин. X ({MinOffsetX}) больше макс. X ({MaxOffsetX}).";
                return false;
            }

            if (MinOffsetY > MaxOffsetY)
            {
                error = $"Мин. Y ({MinOffsetY}) больше макс. Y ({MaxOffsetY}).";
                return false;
            }

            error = null;
            return true;
        }
    }
}
