using System;
using Autodesk.AutoCAD.DatabaseServices;
using CivilGPT.CAD.Interfaces;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Фабрика транзакций, привязанная к конкретной базе данных чертежа.
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly Database _database;

        public TransactionService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public static TransactionService FromActiveDocument()
        {
            Database database = CadApplication.ActiveDatabase;
            if (database == null)
            {
                throw new InvalidOperationException("Нет активного документа AutoCAD.");
            }
            return new TransactionService(database);
        }

        public Transaction Start()
        {
            return _database.TransactionManager.StartTransaction();
        }
    }
}
