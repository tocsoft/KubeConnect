using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.RunAdminProcess
{
    public class ProcessRunner
    {
        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="processStartInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments.</param>
        /// <param name="standardOutput">List that lines written to standard output by the process will be added to</param>
        /// <param name="standardError">List that lines written to standard error by the process will be added to</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static async Task<ProcessResults> RunAsync(ProcessStartInfo processStartInfo, Action<string> writeStandardOutput, Action<string> writeStandardError, CancellationToken cancellationToken)
        {
            // force some settings in the start info so we can capture the output
            var readingOut = writeStandardOutput != null || writeStandardOutput != null;
            if (processStartInfo.Verb.Equals("runas", StringComparison.OrdinalIgnoreCase))
            {
                readingOut = false;
                processStartInfo.UseShellExecute = true;
            }
            else
            {
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
            }
            //processStartInfo.CreateNoWindow = true;
            //processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            var tcs = new TaskCompletionSource<ProcessResults>();

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            var standardOutputResults = new TaskCompletionSource<object>();
            var standardErrorResults = new TaskCompletionSource<object>();
            if (readingOut)
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (args.Data != null)
                            writeStandardOutput?.Invoke(args.Data);
                        else
                            standardOutputResults.TrySetResult(null!);
                    }
                    else
                    {
                        standardOutputResults.TrySetResult(null!);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (args.Data != null)
                            writeStandardError?.Invoke(args.Data);
                        else
                            standardErrorResults.TrySetResult(null!);
                    }
                    else
                    {
                        standardErrorResults.TrySetResult(null!);
                    }
                };
            }
            else
            {
                standardOutputResults.TrySetResult(null!);
                standardErrorResults.TrySetResult(null!);
            }

            var processStartTime = new TaskCompletionSource<DateTime>();

            process.Exited += async (sender, args) =>
            {
                // Since the Exited event can happen asynchronously to the output and error events, 
                // we await the task results for stdout/stderr to ensure they both closed.  We must await
                // the stdout/stderr tasks instead of just accessing the Result property due to behavior on MacOS.  
                // For more details, see the PR at https://github.com/jamesmanning/RunProcessAsTask/pull/16/
                await Task.WhenAll(
                    standardOutputResults.Task,
                    standardErrorResults.Task,
                    processStartTime.Task
                    ).ConfigureAwait(false);

                tcs.TrySetResult(
                    new ProcessResults(
                        process,
                        processStartTime.Task.Result
                    )
                );
            };

            using (cancellationToken.Register(
                () =>
                {
                    tcs.TrySetCanceled();
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (InvalidOperationException) { }
                }))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startTime = DateTime.Now;
                if (process.Start() == false)
                {
                    tcs.TrySetException(new InvalidOperationException("Failed to start process"));
                }
                else
                {
                    ChildProcessTracker.AddProcess(process);
                    try
                    {
                        startTime = process.StartTime;
                    }
                    catch (Exception)
                    {
                        // best effort to try and get a more accurate start time, but if we fail to access StartTime
                        // (for instance, process has already existed), we still have a valid value to use.
                    }
                    processStartTime.SetResult(startTime);
                    if (readingOut)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
