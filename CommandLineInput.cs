using CivilGPT.CAD.Common;
using CivilGPT.Models;

namespace CivilGPT
{
    /// <summary>
    /// Диалог с пользователем через командную строку AutoCAD.
    /// </summary>
    internal static class CommandLineInput
    {
        public static bool TryGetElevationOffsetOptions(
            EditorService editor,
            out ElevationOffsetOptions options)
        {
            options = null;

            double? min = editor.GetDouble("\nМинимальное смещение по высоте, м", -0.02, true);
            if (!min.HasValue) return false;

            double? max = editor.GetDouble("\nМаксимальное смещение по высоте, м", 0.02, true);
            if (!max.HasValue) return false;

            var result = new ElevationOffsetOptions
            {
                MinOffset = min.Value,
                MaxOffset = max.Value
            };

            string error;
            if (!result.IsValid(out error))
            {
                editor.WriteError(error);
                return false;
            }

            options = result;
            return true;
        }

        public static bool TryGetHorizontalOffsetOptions(
            EditorService editor,
            out HorizontalOffsetOptions options)
        {
            options = null;

            double? minX = editor.GetDouble("\nМинимальное смещение по X, м", -0.005, true);
            if (!minX.HasValue) return false;

            double? maxX = editor.GetDouble("\nМаксимальное смещение по X, м", 0.005, true);
            if (!maxX.HasValue) return false;

            double? minY = editor.GetDouble("\nМинимальное смещение по Y, м", -0.005, true);
            if (!minY.HasValue) return false;

            double? maxY = editor.GetDouble("\nМаксимальное смещение по Y, м", 0.005, true);
            if (!maxY.HasValue) return false;

            var result = new HorizontalOffsetOptions
            {
                MinOffsetX = minX.Value,
                MaxOffsetX = maxX.Value,
                MinOffsetY = minY.Value,
                MaxOffsetY = maxY.Value
            };

            string error;
            if (!result.IsValid(out error))
            {
                editor.WriteError(error);
                return false;
            }

            options = result;
            return true;
        }

        public static bool TryGetPointCreationOptions(
            EditorService editor,
            out PointCreationOptions options)
        {
            options = null;

            string groupName = editor.GetString("\nИмя новой группы точек");
            if (groupName == null) return false;

            double? minX = editor.GetDouble("\nМинимальное смещение по X, м", -0.05, true);
            if (!minX.HasValue) return false;

            double? maxX = editor.GetDouble("\nМаксимальное смещение по X, м", 0.05, true);
            if (!maxX.HasValue) return false;

            double? minY = editor.GetDouble("\nМинимальное смещение по Y, м", -0.05, true);
            if (!minY.HasValue) return false;

            double? maxY = editor.GetDouble("\nМаксимальное смещение по Y, м", 0.05, true);
            if (!maxY.HasValue) return false;

            double? minZ = editor.GetDouble("\nМинимальное смещение по Z, м", -0.02, true);
            if (!minZ.HasValue) return false;

            double? maxZ = editor.GetDouble("\nМаксимальное смещение по Z, м", 0.05, true);
            if (!maxZ.HasValue) return false;

            var result = new PointCreationOptions
            {
                GroupName = groupName,
                MinOffsetX = minX.Value,
                MaxOffsetX = maxX.Value,
                MinOffsetY = minY.Value,
                MaxOffsetY = maxY.Value,
                MinOffsetZ = minZ.Value,
                MaxOffsetZ = maxZ.Value
            };

            string error;
            if (!result.IsValid(out error))
            {
                editor.WriteError(error);
                return false;
            }

            options = result;
            return true;
        }
    }
}
