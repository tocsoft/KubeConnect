using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public interface IConsole
    {
        void WriteErrorLine(string line);
        void WriteLine(string line);

        event ConsoleCancelEventHandler CancelKeyPress;
    }

    public delegate void ConsoleCancelEventHandler(object? sender, EventArgs e);
}
