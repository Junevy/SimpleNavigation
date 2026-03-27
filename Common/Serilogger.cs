using System.Windows;
using SimpleNavigation.Interface;

namespace SimpleNavigation.Common
{
    /// <summary>
    /// Serilog日志记录器，用于全局日志记录。
    /// </summary>
    public sealed class Serilogger : IDisposable
    {
        private bool disposed = false;
        private static readonly object loggerLock = new();
        private static int _instanceSet = 0;

        private static volatile ISerilog? _instance;
        public static ISerilog Instance 
        {
            get
            {
                if (_instance == null)
                    throw new ArgumentNullException($"{nameof(_instance)} is null.");
                return _instance;
            } 
            private set => _instance = value;
        }

        private Serilogger() { }

        public static void SetInstance(ISerilog logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            lock(loggerLock)
            {
                if (Instance != null)
                {
                    Interlocked.Exchange(ref _instanceSet, 0);
                    throw new InvalidOperationException($"{nameof(Instance)} has been initialized!");
                }

                Instance = logger;
            }
        }

        public void Dispose()
        {
            if (disposed)
                 return;

 
             GC.SuppressFinalize(this);
             Instance.Dispose();

             disposed = true;
             _instance = null;
             Interlocked.Exchange(ref _instanceSet, 0);
        }
    }
}