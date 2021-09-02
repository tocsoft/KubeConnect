using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.RunAdminProcess
{
    public class IPCServiceForwarder
    {
        private readonly IConsole console;
        private IPCService? connection = null;

        public string PipeName { get; } = Guid.NewGuid().ToString();

        public IPCServiceForwarder(IConsole console)
        {
            this.console = console;
        }

        public void Listen(CancellationToken cancellationToken)
        {
            this.connection = new IPCService(PipeName, true);

            connection.Connect(cancellationToken);

            cancellationToken.Register(() =>
            {
                connection.Send("SIG_KILL");
            });

            this.connection.RegisterHandler(p =>
            {
                var type = p.Substring(0, 1);
                var message = p.Substring(2);

                switch (type)
                {
                    case "e":
                        console.WriteErrorLine(message);
                        break;
                    case "l":
                    case "w":
                    default:
                        console.WriteLine(message);
                        break;
                }
            });
        }
    }
}
