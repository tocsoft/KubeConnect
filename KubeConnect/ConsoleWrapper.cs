using System;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class ConsoleWrapper : IConsole
    {
        object locker = new object();
        private event ConsoleCancelEventHandler _cancelKeyPress;
        public event ConsoleCancelEventHandler CancelKeyPress
        {
            add
            {
                _cancelKeyPress += value;
                ListenToCancel();
            }
            remove
            {
                _cancelKeyPress -= value;
                ListenToCancel(); ;
            }
        }

        private void listener(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Task.Run(() =>
            {
                _cancelKeyPress?.Invoke(sender, e);
            });
        }

        private bool listening = false;
        private void ListenToCancel()
        {
            var hasListeners = (_cancelKeyPress?.GetInvocationList()?.Length ?? 0) > 0;

            if (listening && !hasListeners)
            {
                listening = false;
                Console.CancelKeyPress -= listener;
            }
            else if (!listening && hasListeners)
            {
                listening = true;
                Console.CancelKeyPress += listener;
            }
        }

        public void WriteErrorLine(string line)
        {
            lock (locker)
            {
                Console.Error.WriteLine(line);
            }
        }

        public void WriteLine(string line)
        {
            lock (locker)
            {
                Console.WriteLine(line);
            }
        }
    }
}