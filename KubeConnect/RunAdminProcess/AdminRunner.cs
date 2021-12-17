using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.RunAdminProcess
{
    public static class AdminRunner
    {
        public static async Task<int> RunProcessAsAdmin(Args parseArgs, IConsole console)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (parseArgs.Elevated)
                {
                    // this should already be elevated, guess if we are not somethign went wrong and we should probably tell them to use an admin prompt
                    console.WriteErrorLine("Error: must be ran from an administrator command prompt");
                    return -1;
                }
                var forwarder = new IPCServiceForwarder(console);
                var commandline = Environment.CommandLine;

                commandline += $" --elevated-command {forwarder.PipeName} ";
                if (Debugger.IsAttached)
                {
                    commandline += "--attach-debugger ";
                }

                var cts = new CancellationTokenSource();

                console.CancelKeyPress += delegate
                {
                    cts.Cancel();
                };
                forwarder.Listen(cts.Token);

                var exeToRun = Process.GetCurrentProcess()?.MainModule?.FileName;
                if (exeToRun == null)
                {
                    throw new InvalidProgramException("can't find exe to elevate");
                }

                var info = new ProcessStartInfo(exeToRun, commandline)
                {
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                // we want a chance for the process to shutdown gracefully before we kill it
                var processCts = new CancellationTokenSource();
                cts.Token.Register(() =>
                {
                    processCts.CancelAfter(10000);// give process 10 seconds to cancels after cancerlatino is triggered via listener
                });


                try
                {
                    var resTask = ProcessRunner.RunAsync(info,
                        console.WriteLine,
                        console.WriteErrorLine,
                    processCts.Token);

                    var res = await resTask;

                    Environment.Exit(res.ExitCode);
                    return res.ExitCode;
                }
                catch
                {
                    console.WriteErrorLine("Error: we require running as administrator");
                    Environment.Exit(1);
                    return 1;
                }
            }
            else
            {
                console.WriteErrorLine("Error: must be ran as root, rerun the command via 'sudo'");
            }
            return -1;
        }

    }
}
