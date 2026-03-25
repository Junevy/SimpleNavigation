namespace SimpleNavigation.Interface
{
    public interface ISerilog : IDisposable
    {
        void Verbose();

        void Debug();

        void Information();

        void Warning();

        void Error();

        void Fatal();
    }
}

