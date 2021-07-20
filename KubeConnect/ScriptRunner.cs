using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    internal class ScriptRunner : IDisposable
    {
        private Process? _process;

        public EventedStreamReader StdOut { get; }

        public EventedStreamReader StdErr { get; }

        private static readonly Regex AnsiColorRegex = new Regex("\x001b\\[[0-9;]*m", RegexOptions.None, TimeSpan.FromSeconds(1));

        public ScriptRunner(string workingDirectory, string script, IDictionary<string, string>? envVars, CancellationToken applicationStoppingToken)
        {
            if (string.IsNullOrEmpty(workingDirectory))
            {
                throw new ArgumentException("Cannot be null or empty.", nameof(workingDirectory));
            }

            var exeToRun = "bash";
            var completeArguments = $"-c {script}";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exeToRun = "cmd";
                completeArguments = $"/c {script}";
            }

            var processStartInfo = new ProcessStartInfo(exeToRun)
            {
                Arguments = completeArguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };

            if (envVars != null)
            {
                foreach (var keyValuePair in envVars)
                {
                    processStartInfo.Environment[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            _process = LaunchProcess(processStartInfo, script);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ChildProcessTracker.AddProcess(_process);
            }

            StdOut = new EventedStreamReader(_process.StandardOutput);
            StdErr = new EventedStreamReader(_process.StandardError);

            applicationStoppingToken.Register(((IDisposable)this).Dispose);
        }

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_process?.HasExited != false)
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        //public void AttachToLogger(logger)
        //{
        //    // When the node task emits complete lines, pass them through to the real logger
        //    StdOut.OnReceivedLine += line =>
        //    {
        //        if (!string.IsNullOrWhiteSpace(line))
        //        {
        //            // Node tasks commonly emit ANSI colors, but it wouldn't make sense to forward
        //            // those to loggers (because a logger isn't necessarily any kind of terminal)
        //            logger.LogInformation(StripAnsiColors(line).TrimEnd());
        //        }
        //    };

        //    StdErr.OnReceivedLine += line =>
        //    {
        //        if (!string.IsNullOrWhiteSpace(line))
        //        {
        //            logger.LogError(StripAnsiColors(line).TrimEnd());
        //        }
        //    };

        //    // But when it emits incomplete lines, assume this is progress information and
        //    // hence just pass it through to StdOut regardless of logger config.
        //    StdErr.OnReceivedChunk += chunk =>
        //    {
        //        Debug.Assert(chunk.Array != null, "Array should never be null");

        //        var containsNewline = Array.IndexOf(
        //            chunk.Array, '\n', chunk.Offset, chunk.Count) >= 0;
        //        if (!containsNewline)
        //        {
        //            Console.Write(chunk.Array, chunk.Offset, chunk.Count);
        //        }
        //    };
        //}

        public void AttachToConsole()
        {
            // When the node task emits complete lines, pass them through to the real logger
            StdOut.OnReceivedLine += line =>
            {
                // Node tasks commonly emit ANSI colors, but it wouldn't make sense to forward
                // those to loggers (because a logger isn't necessarily any kind of terminal)
                Console.WriteLine(StripAnsiColors(line));
            };

            StdErr.OnReceivedLine += line =>
            {
                Console.Error.WriteLine(StripAnsiColors(line));
            };

            // But when it emits incomplete lines, assume this is progress information and
            // hence just pass it through to StdOut regardless of logger config.
            StdErr.OnReceivedChunk += chunk =>
            {
                Debug.Assert(chunk.Array != null, "Array should never be null");

                var containsNewline = Array.IndexOf(
                    chunk.Array, '\n', chunk.Offset, chunk.Count) >= 0;
                if (!containsNewline)
                {
                    Console.Write(chunk.Array, chunk.Offset, chunk.Count);
                }
            };
        }

        private static string StripAnsiColors(string line)
            => AnsiColorRegex.Replace(line, string.Empty);

        private static Process LaunchProcess(ProcessStartInfo startInfo, string commandName)
        {
            try
            {
                var process = Process.Start(startInfo);

                // See equivalent comment in OutOfProcessNodeInstance.cs for why
                process.EnableRaisingEvents = true;

                return process;
            }
            catch (Exception ex)
            {
                var message = $"Failed to start '{commandName}'. To resolve this:.\n\n"
                            + $"[1] Ensure that '{commandName}' is installed and can be found in one of the PATH directories.\n"
                            + $"    Current PATH environment variable is: {Environment.GetEnvironmentVariable("PATH")}\n"
                            + "    Make sure the executable is in one of those directories, or update your PATH.\n\n"
                            + "[2] See the InnerException for further details of the cause.";
                throw new InvalidOperationException(message, ex);
            }
        }

        void IDisposable.Dispose()
        {
            this.Stop();
        }

        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process = null;
            }
        }
    }

    internal class EventedStreamReader
    {
        public delegate void OnReceivedChunkHandler(ArraySegment<char> chunk);

        public delegate void OnReceivedLineHandler(string line);

        public delegate void OnStreamClosedHandler();

        public event OnReceivedChunkHandler? OnReceivedChunk;

        public event OnReceivedLineHandler? OnReceivedLine;

        public event OnStreamClosedHandler? OnStreamClosed;

        private readonly StreamReader _streamReader;
        private readonly StringBuilder _linesBuffer;

        public EventedStreamReader(StreamReader streamReader)
        {
            _streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
            _linesBuffer = new StringBuilder();
            Task.Factory.StartNew(Run);
        }

        public Task<Match> WaitForMatch(Regex regex)
        {
            var tcs = new TaskCompletionSource<Match>();
            var completionLock = new object();

            OnReceivedLineHandler? onReceivedLineHandler = null;
            OnStreamClosedHandler? onStreamClosedHandler = null;

            void ResolveIfStillPending(Action applyResolution)
            {
                lock (completionLock)
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        OnReceivedLine -= onReceivedLineHandler;
                        OnStreamClosed -= onStreamClosedHandler;
                        applyResolution();
                    }
                }
            }

            onReceivedLineHandler = line =>
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    ResolveIfStillPending(() => tcs.SetResult(match));
                }
            };

            onStreamClosedHandler = () =>
            {
                ResolveIfStillPending(() => tcs.SetException(new EndOfStreamException()));
            };

            OnReceivedLine += onReceivedLineHandler;
            OnStreamClosed += onStreamClosedHandler;

            return tcs.Task;
        }

        private async Task Run()
        {
            var buf = new char[8 * 1024];
            while (true)
            {
                var chunkLength = await _streamReader.ReadAsync(buf, 0, buf.Length);
                if (chunkLength == 0)
                {
                    if (_linesBuffer.Length > 0)
                    {
                        OnCompleteLine(_linesBuffer.ToString());
                        _linesBuffer.Clear();
                    }

                    OnClosed();
                    break;
                }

                OnChunk(new ArraySegment<char>(buf, 0, chunkLength));

                int lineBreakPos;
                int startPos = 0;

                // get all the newlines
                while ((lineBreakPos = Array.IndexOf(buf, '\n', startPos, chunkLength - startPos)) >= 0 && startPos < chunkLength)
                {
                    var length = (lineBreakPos + 1) - startPos;
                    _linesBuffer.Append(buf, startPos, length);
                    OnCompleteLine(_linesBuffer.ToString());
                    _linesBuffer.Clear();
                    startPos = lineBreakPos + 1;
                }

                // get the rest
                if (lineBreakPos < 0 && startPos < chunkLength)
                {
                    _linesBuffer.Append(buf, startPos, chunkLength - startPos);
                }
            }
        }

        private void OnChunk(ArraySegment<char> chunk)
        {
            var dlg = OnReceivedChunk;
            dlg?.Invoke(chunk);
        }

        private void OnCompleteLine(string line)
        {
            var dlg = OnReceivedLine;
            dlg?.Invoke(line);
        }

        private void OnClosed()
        {
            var dlg = OnStreamClosed;
            dlg?.Invoke();
        }
    }

#pragma warning disable SA1121 // Use built-in type alias
    /// <summary>
    /// Allows processes to be automatically killed if this parent process unexpectedly quits.
    /// This feature requires Windows 8 or greater. On Windows 7, nothing is done.</summary>
    /// <remarks>References:
    ///  https://stackoverflow.com/a/4657392/386091
    ///  https://stackoverflow.com/a/9164742/386091 </remarks>
    internal static class ChildProcessTracker
    {
        /// <summary>
        /// Add the process to be tracked. If our current process is killed, the child processes
        /// that we are tracking will be automatically killed, too. If the child process terminates
        /// first, that's fine, too.</summary>
        /// <param name="process"></param>
        public static void AddProcess(Process process)
        {
            if (_jobHandle != IntPtr.Zero)
            {
                bool success = AssignProcessToJobObject(_jobHandle, process.Handle);
                if (!success && !process.HasExited)
                {
                    throw new Win32Exception();
                }
            }
        }

        static ChildProcessTracker()
        {
            // This feature requires Windows 8 or later. To support Windows 7 requires
            //  registry settings to be added if you are using Visual Studio plus an
            //  app.manifest change.
            //  https://stackoverflow.com/a/4232259/386091
            //  https://stackoverflow.com/a/9507862/386091
            if (Environment.OSVersion.Version < new Version(6, 2))
            {
                return;
            }

            // The job name is optional (and can be null) but it helps with diagnostics.
            //  If it's not null, it has to be unique. Use SysInternals' Handle command-line
            //  utility: handle -a ChildProcessTracker
            string jobName = "ChildProcessTracker" + Process.GetCurrentProcess().Id;
            _jobHandle = CreateJobObject(IntPtr.Zero, jobName);

            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                // This is the key flag. When our process is killed, Windows will automatically
                //  close the job handle, and when that happens, we want the child processes to
                //  be killed, too.
                LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = info
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr job, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        // Windows will automatically close any open job handles when our process terminates.
        //  This can be verified by using SysInternals' Handle utility. When the job handle
        //  is closed, the child processes will be killed.
        private static readonly IntPtr _jobHandle;

        public enum JobObjectInfoType
        {
            AssociateCompletionPortInformation = 7,
            BasicLimitInformation = 2,
            BasicUIRestrictions = 4,
            EndOfJobTimeInformation = 6,
            ExtendedLimitInformation = 9,
            SecurityLimitInformation = 5,
            GroupInformation = 11
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public Int64 PerProcessUserTimeLimit;
            public Int64 PerJobUserTimeLimit;
            public JOBOBJECTLIMIT LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public UInt32 ActiveProcessLimit;
            public Int64 Affinity;
            public UInt32 PriorityClass;
            public UInt32 SchedulingClass;
        }

        [Flags]
        private enum JOBOBJECTLIMIT : uint
        {
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
#pragma warning restore SA1121 // Use built-in type alias
}
