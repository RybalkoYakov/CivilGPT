using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Точка доступа к текущему состоянию AutoCAD / Civil 3D.
    /// Все методы рассчитаны на вызов из главного потока приложения.
    /// </summary>
    public static class CadApplication
    {
        public static bool HasActiveDocument => AcApp.DocumentManager?.MdiActiveDocument != null;

        public static Document ActiveDocument => AcApp.DocumentManager?.MdiActiveDocument;

        public static Database ActiveDatabase => ActiveDocument?.Database;

        public static Editor ActiveEditor => ActiveDocument?.Editor;

        /// <summary>
        /// Активный документ Civil 3D. Возвращает null, если запущен «чистый» AutoCAD
        /// или документ не инициализирован как Civil-документ.
        /// </summary>
        public static CivilDocument ActiveCivilDocument
        {
            get
            {
                try
                {
                    return HasActiveDocument ? CivilApplication.ActiveDocument : null;
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
        }

        public static bool IsCivilAvailable => ActiveCivilDocument != null;

        /// <summary>
        /// Матрица перехода из текущей ПСК в МСК.
        /// </summary>
        public static Matrix3d ActiveUcsToWcs
        {
            get
            {
                Editor editor = ActiveEditor;
                return editor != null ? editor.CurrentUserCoordinateSystem : Matrix3d.Identity;
            }
        }

        public static void WriteMessage(string message)
        {
            ActiveEditor?.WriteMessage(message ?? string.Empty);
        }
    }
}
