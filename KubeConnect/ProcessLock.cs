using System;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class ProcessLock : IDisposable
    {
        private NamedPipeServerStream? pipe;
        private readonly string name;

        public ProcessLock(string name)
        {
            this.name = name;
            this.pipe = GetPipe();
        }

        private NamedPipeServerStream? GetPipe()
        {
            try
            {
                return new NamedPipeServerStream(name, PipeDirection.InOut, 1);
            }
            catch (IOException)
            {
                return null;
            }
        }

        public void OnLock(Action callback)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    var pipe = GetPipe();
                    if (pipe != null)
                    {
                        pipe.Dispose();
                        callback();
                    }

                    await Task.Delay(100);
                }
            });
        }

        public bool Locked => pipe != null;

        public void Dispose()
        {
            pipe?.Dispose();
            pipe = null;
        }
    }
}
