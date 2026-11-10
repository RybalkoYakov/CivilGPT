using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Interfaces
{
    /// <summary>
    /// Фабрика транзакций чертежа. Позволяет тестировать логику без прямого доступа к Editor.
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>Открыть новую транзакцию. Вызывающий код отвечает за Commit и Dispose.</summary>
        Transaction Start();
    }
}
