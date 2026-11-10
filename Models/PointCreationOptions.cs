namespace CivilGPT.Models
{
    /// <summary>
    /// Параметры создания новых точек COGO со случайным смещением
    /// в заданных диапазонах по осям текущей ПСК.
    /// </summary>
    public class PointCreationOptions
    {
        public string GroupName { get; set; } = "";

        public double MinOffsetX { get; set; } = -0.05;

        public double MaxOffsetX { get; set; } = 0.05;

        public double MinOffsetY { get; set; } = -0.05;

        public double MaxOffsetY { get; set; } = 0.05;

        public double MinOffsetZ { get; set; } = -0.05;

        public double MaxOffsetZ { get; set; } = 0.05;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                error = "Имя группы точек не может быть пустым.";
                return false;
            }

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

            if (MinOffsetZ > MaxOffsetZ)
            {
                error = $"Мин. Z ({MinOffsetZ}) больше макс. Z ({MaxOffsetZ}).";
                return false;
            }

            error = null;
            return true;
        }
    }
}
