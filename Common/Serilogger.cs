namespace SimpleNavigation.Common
{
    /// <summary>
    /// Discarded class.
    /// </summary>
    public sealed class Serilogger : IDisposable
    {
        private bool disposed = false;
        private static readonly object loggerLock = new();
        private static int _instanceSet = 0;

        private static volatile ISerilog _instance;
        public static ISerilog Instance 
        {
            get
            {
                if (_instance == null)
                    throw new ArgumentNullException($"{nameof(_instance)} is null.");
                return _instance;
            } 
        }

        private Serilogger() { }

        public static void SetInstance(ISerilog logger)
        {
            if (logger == null) throw new ArgumentNullException($"{nameof(logger)} is null.");

            if (Interlocked.CompareExchange(ref _instanceSet, 1, 0) != 0)
            {
                throw InvalidaOperationException($"{nameof(Instance)} has been initialized!");
            }

            lock(loggerLock)
            {
                if (Instance != null)
                {
                    Interlocked.Exchange(ref _instanceSet, 0);
                    throw InvalidaOperationException($"{nameof(Instance)} has been initialized!");
                }

                this.Instance = logger;
            }
        }

        private void Dispose()
        {
            if (disposed)
                return;
            
            // Dispose(true);
            GC.SuppressFinalize(this);
            Instance.Dispose();

            disposed = true;
            _instance = null;
            Interlocked.Exchange(ref _instanceSet, 0);
        }
    }
}