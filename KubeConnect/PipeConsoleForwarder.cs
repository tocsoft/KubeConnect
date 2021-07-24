using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class PipeConsoleForwarder
    {
        private readonly IConsole console;

        public string PipeName { get; } = Guid.NewGuid().ToString();
        public PipeConsoleForwarder(IConsole console)
        {
            this.console = console;
        }

        public Task Listen(CancellationToken cancellationToken)
        => Task.Run(async () =>
            {
                var srv = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                srv.ConfigureAwait(false);
                try
                {
                    await srv.WaitForConnectionAsync(cancellationToken);

                    // Read the request from the client. Once the client has
                    // written to the pipe its security token will be available.
                    StreamString ss = new StreamString(srv);
                    cancellationToken.Register(() =>
                    {
                        ss.WriteString("k", "");
                    });
                    // Verify our identity to the connected client using a
                    // string that the client anticipates.
                    while (srv.IsConnected)
                    {

                        var (type, message) = ss.ReadString();
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
                    }
                }
                catch (Exception ex)
                {
                    var t = ex;
                }
            });
    }
}
