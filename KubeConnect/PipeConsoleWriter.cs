using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class PipeConsoleWriter : IConsole, IDisposable
    {
        private readonly NamedPipeClientStream client;
        private StreamString simpleClient;
        private Task task;

        public PipeConsoleWriter(string pipeName)
        {
            this.client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            this.client.Connect();
            this.simpleClient = new StreamString(client);
            this.task = Task.Run(() =>
            {
                while (this.client.IsConnected)
                {
                    var (type, _) = simpleClient.ReadString();
                    if (type == "k")
                    {
                        new Thread(()=>
                        {
                            try
                            {
                                CancelKeyPress?.Invoke(this, new EventArgs());
                            }
                            catch
                            {
                                // noop
                            }
                        }).Start();
                    }
                }
            });
        }

        public event ConsoleCancelEventHandler CancelKeyPress;

        public void Dispose()
        {
            client.Dispose();
        }

        public void Write(string text)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("w", text);
            }
        }

        public void WriteErrorLine(string line)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("e", line);
            }
        }

        public void WriteLine(string line)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("l", line);
            }
        }
    }
}
