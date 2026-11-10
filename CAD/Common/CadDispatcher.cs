using System;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Единая точка маршалинга вызовов в главный поток AutoCAD/Civil 3D.
    /// Нужна потому, что API Civil 3D не является потокобезопасным:
    /// обращения к документу допустимы только из контекста приложения.
    /// </summary>
    /// <remarks>
    /// ВАЖНО: вызывать только из фонового потока (например, из обработчика панели ИИ).
    /// Вызов из главного потока AutoCAD приведёт к ожиданию по таймауту.
    /// </remarks>
    public static class CadDispatcher
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        public static T Execute<T>(Func<T> func)
        {
            return Execute(func, DefaultTimeout);
        }

        public static T Execute<T>(Func<T> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            Document document = AcApp.DocumentManager?.MdiActiveDocument;
            if (document == null)
            {
                throw new InvalidOperationException("Нет активного документа AutoCAD.");
            }

            T result = default(T);
            System.Exception failure = null;

            using (var completed = new ManualResetEventSlim(false))
            {
                // ExecuteInApplicationContext ставит обработчик в очередь контекста приложения.
                // Без ожидания result был бы прочитан до фактического выполнения.
                AcApp.DocumentManager.ExecuteInApplicationContext(
                    data =>
                    {
                        try
                        {
                            result = func();
                        }
                        catch (System.Exception ex)
                        {
                            failure = ex;
                        }
                        finally
                        {
                            completed.Set();
                        }
                    },
                    null);

                if (!completed.Wait(timeout))
                {
                    throw new TimeoutException(
                        "Не удалось выполнить операцию в контексте AutoCAD за " +
                        $"{timeout.TotalSeconds:F0} с. " +
                        "Выполнение из главного потока AutoCAD не поддерживается.");
                }
            }

            if (failure != null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }

            return result;
        }

        public static void Execute(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Execute<object>(() => { action(); return null; });
        }
    }
}
