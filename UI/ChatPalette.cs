using System;
using Autodesk.AutoCAD.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CivilGPT.UI
{
    /// <summary>
    /// Панель CivilGPT в AutoCAD (dockable PaletteSet).
    /// PaletteSet хранится в статическом поле, иначе будет собран сборщиком мусора.
    /// </summary>
    public static class ChatPalette
    {
        private static readonly Guid PaletteGuid = new Guid("6E1E9C43-2C0F-4C43-9F5B-1E4A0F2D7B11");

        private static PaletteSet _paletteSet;

        public static void Show()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("CivilGPT", "CivilGPT", PaletteGuid);
                _paletteSet.AddVisual("CivilGPT", new ChatPanel());
            }

            _paletteSet.Visible = true;
        }

        public static void Hide()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Visible = false;
            }
        }
    }
}
