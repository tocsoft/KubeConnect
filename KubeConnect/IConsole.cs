using System;

namespace KubeConnect
{
    public interface IConsole
    {
        void WriteErrorLine(string line);
        void WriteLine(string line);

        event ConsoleCancelEventHandler CancelKeyPress;
    }


    public class LinePrefixingConsole : IConsole
    {
        private readonly string linePrefix;
        private readonly IConsole console;

        public LinePrefixingConsole(string linePrefix, IConsole console)
        {
            this.linePrefix = linePrefix;
            this.console = console;
        }

        public event ConsoleCancelEventHandler CancelKeyPress
        {
            add => console.CancelKeyPress += value;
            remove => console.CancelKeyPress -= value;
        }

        public void WriteErrorLine(string line)
        {
            console.WriteErrorLine(linePrefix + line);
        }

        public void WriteLine(string line)
        {
            console.WriteLine(linePrefix + line);
        }
    }
    public delegate void ConsoleCancelEventHandler(object? sender, EventArgs e);
}
