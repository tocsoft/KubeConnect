using System;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Threading;

namespace KubeConnect
{
    public class ProcessLock : IDisposable
    {
        private Mutex? mutex;
        private readonly string name;

        public ProcessLock(string name)
        {
            this.name = name;
            this.mutex = GetLock();
        }

        private Mutex? GetLock()
        {
            try
            {
                var mutex = new Mutex(false, $"Global\\{name}");
                if (mutex.WaitOne(1))
                {
                    return mutex;
                }
                return null;
            }
            catch
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
                    var mutex = GetLock();
                    if (mutex != null)
                    {
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                        callback();
                    }

                    await Task.Delay(100);
                }
            });
        }

        public bool Locked => mutex != null;

        public void Dispose()
        {
            mutex?.ReleaseMutex();
            mutex = null;
        }
    }
}
