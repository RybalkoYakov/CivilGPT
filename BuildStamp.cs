using System;
using System.Reflection;

namespace CivilGPT
{
    /// <summary>
    /// Штамп сборки. Нужен, чтобы отличить свежую сборку плагина от старой:
    /// AutoCAD не перезагружает уже загруженную DLL, и без штампа невозможно
    /// понять, какой код выполняется.
    /// </summary>
    internal static class BuildStamp
    {
        private static readonly string Cached = Read();

        public static string Value => Cached;

        private static string Read()
        {
            try
            {
                var attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(BuildStamp).Assembly,
                    typeof(AssemblyInformationalVersionAttribute));

                return attribute != null && !string.IsNullOrWhiteSpace(attribute.InformationalVersion)
                    ? attribute.InformationalVersion
                    : "неизвестно";
            }
            catch (Exception)
            {
                return "неизвестно";
            }
        }
    }
}
