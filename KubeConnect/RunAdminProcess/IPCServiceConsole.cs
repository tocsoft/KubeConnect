using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.RunAdminProcess
{
    public class IPCServiceConsole : IConsole, IDisposable
    {
        private IPCService connection;

        public IPCServiceConsole(string pipeName)
        {
            connection = new IPCService(pipeName);

            connection.RegisterHandler((msg) =>
            {
                if (msg == "SIG_KILL")
                {
                    Task.Run(() => CancelKeyPress?.Invoke(this, new EventArgs()));
                }
            });

            connection.Connect(default);
        }

        public event ConsoleCancelEventHandler? CancelKeyPress;

        public void Dispose()
        {
            connection.Dispose();
        }

        public void Write(string text)
        {
            connection.Send("w@" + text);
        }

        public void WriteErrorLine(string line)
        {
            connection.Send("e@" + line);
        }

        public void WriteLine(string line)
        {
            connection.Send("l@" + line);
        }
    }
}
