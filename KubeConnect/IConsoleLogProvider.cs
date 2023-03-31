using Microsoft.Extensions.Logging;
using System;

namespace KubeConnect
{
    public partial class IConsoleLogProvider : ILoggerProvider
    {
        private readonly IConsole console;
        private readonly Args args;

        public IConsoleLogProvider(IConsole console, Args args)
        {
            this.console = console;
            this.args = args;
        }

        public ILogger CreateLogger(string categoryName)
            => new IConsoleLoggger(categoryName, console, this.args.EnableTraceLogs);

        public void Dispose()
        {
        }

        public class IConsoleLoggger : ILogger, IDisposable
        {
            public IConsoleLoggger(string category, IConsole console, bool traceLogs)
            {
                this.traceLogs = traceLogs;
                Category = category;
                this.console = console;
                this.internalLogs = category.StartsWith("KubeConnect");
            }

            private readonly bool traceLogs;

            public string Category { get; }

            private bool internalLogs;
            private readonly IConsole console;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return this;
            }

            public void Dispose()
            {
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                if (traceLogs)
                {
                    return true;
                }

                if (internalLogs)
                {
                    return logLevel >= LogLevel.Debug;
                }

                return logLevel >= LogLevel.Warning;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    console.WriteLine(formatter(state, exception));
                }
            }
        }
    }
}
