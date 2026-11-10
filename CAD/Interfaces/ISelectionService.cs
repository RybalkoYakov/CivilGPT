using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Interfaces
{
    /// <summary>
    /// Доступ к выделению объектов в чертеже.
    /// </summary>
    public interface ISelectionService
    {
        /// <summary>Интерактивный выбор объектов с фильтром по DXF-типам.</summary>
        ObjectId[] GetSelection(string message, params string[] dxfTypes);

        /// <summary>Ранее выбранные (implied) объекты, без запроса у пользователя.</summary>
        ObjectId[] GetImpliedSelection();

        /// <summary>Сначала implied-выделение, если пусто — интерактивный выбор.</summary>
        ObjectId[] GetSelectionOrImplied(string message, params string[] dxfTypes);
    }
}
