using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace KubeConnect
{
    public class Args
    {
        Dictionary<string, string> shotNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = "namespace",
            ["l"] = "launch",
            ["x"] = "context",
            ["m"] = "map",
        };

        KubeConnectMode defaultMode = KubeConnectMode.Connect;
        Dictionary<string, KubeConnectMode> mode = new Dictionary<string, KubeConnectMode>(StringComparer.OrdinalIgnoreCase)
        {
            ["runas"] = KubeConnectMode.Run,
            ["connect"] = KubeConnectMode.Connect,
            ["bridge"] = KubeConnectMode.Bridge,
        };

        List<string> optionsWithArguments = new List<string>()
            {
                "service",
                "namespace",
                "elevated-command",
                "context",
                "map",
                "kubeconfig",
                "env",
                "working-directory"
            };

        public Args(string[] args)
        {
            this.RemainingArgs = Array.Empty<string>();
            Func<string, string, string, bool> processArg = (option, arg, argNext) =>
            {
                switch (option)
                {
                    case "launch":
                        LaunchBrowser = true;
                        break;
                    case "namespace":
                        Namespace = argNext;
                        break;
                    case "no-logo":
                        NoLogo = true;
                        break;
                    case "elevated-command":
                        NoLogo = true;
                        Elevated = true;
                        ConsolePipeName = argNext;
                        break;
                    case "attach-debugger":
                        AttachDebugger = true;
                        break;
                    case "context":
                        Context = argNext;
                        break;
                    case "map":
                        Mappings.Add(new Mapping(argNext));
                        break;
                    case "kubeconfig":
                        KubeconfigFile = argNext;
                        break;
                    case "skip-hosts":
                        UpdateHosts = false;
                        break;
                    case "http-only":
                        UseSsl = false;
                        break;
                    case "service":
                        BridgeMappings.Add(new BridgeMapping(argNext));
                        break;
                    case "env":
                        EnvVars.Add(new EnvVarMapping(argNext));
                        break;
                    case "forward-mapped-only":
                        AllServices = false;
                        break;
                    case "working-directory":
                        WorkingDirectory = Path.GetFullPath(argNext);
                        break;
                    case "trace-logs":
                        EnableTraceLogs = true;
                        break;
                    default:
                        // capture unknown args
                        if (!string.IsNullOrWhiteSpace(arg))
                        {
                            return false;
                        }
                        break;
                }

                // captured
                return true;
            };

            ProcessArgs(args, processArg, out var remainingSet, out var unprocessedArgs);

            this.UnprocessedArgs = unprocessedArgs;
            // find mode and trim it off if we need to
            var span = remainingSet.ToArray().AsSpan();
            this.Action = defaultMode;
            if (span.Length > 0)
            {
                if (mode.TryGetValue(span[0], out var connectMode))
                {
                    this.Action = connectMode;
                    span = span.Slice(1);// trim start
                }
            }
            /*
             kubeconnect runas --bridge landscape-explorer --envvar -ASPNETCORE_URLS -- dotnet run --project 
             */

            this.RemainingArgs = span.ToArray();
        }

        private void ProcessArgs(string[] args, Func<string, string, string, bool> processArg, out List<string> remainingSet, out string[] unprocessedArgs)
        {
            remainingSet = new List<string>(args.Length);
            unprocessedArgs = Array.Empty<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == null)
                {
                    continue;
                }

                if (a == "--")
                {
                    if (i + 1 < args.Length)
                    {
                        // capture all args after '--' so that we can do stuff with it
                        unprocessedArgs = args.AsSpan().Slice(i + 1).ToArray();
                    }
                    break;
                }

                string option = "";
                if (a.StartsWith("-") || a.StartsWith("--"))
                {
                    option = a.TrimStart('-');
                }

                var sepIdx = option.IndexOf('=');
                var argNext = i + 1 < args.Length ? args[i + 1] : "";
                var offsetI = 1;
                if (sepIdx > 0)
                {
                    argNext = option.Substring(sepIdx + 1);
                    option = option.Substring(0, sepIdx);
                    offsetI = 0;
                }
                option = option.ToLower();

                // map from short to long name!!
                if (shotNameMap.TryGetValue(option, out var overideName))
                {
                    option = overideName;
                }

                // discover if we want to consume the next token or not
                if (!optionsWithArguments.Contains(option))
                {
                    offsetI = 0;
                }
                // consume the next value if we have to
                i += offsetI;
                if (!processArg(option, a, argNext))
                {
                    remainingSet.Add(a);
                }

            }
        }

        public bool EnableTraceLogs { get; set; } = false;
        public string? Namespace { get; set; }
        public string? KubeconfigFile { get; private set; }
        public string? Context { get; private set; }
        public List<Mapping> Mappings { get; } = new List<Mapping>();
        public string? ConsolePipeName { get; private set; }
        public bool NoLogo { get; private set; }
        public bool Elevated { get; private set; }
        public bool AttachDebugger { get; private set; }
        public bool LaunchBrowser { get; private set; } = false;
        public bool UpdateHosts { get; private set; } = true;
        public bool UseSsl { get; private set; } = true;
        public bool AllServices { get; private set; } = true;
        public string WorkingDirectory { get; private set; } = Directory.GetCurrentDirectory();

        public bool RequireAdmin => (UpdateHosts || UseSsl) && Action == KubeConnectMode.Connect;

        public List<BridgeMapping> BridgeMappings { get; } = new List<BridgeMapping>();
        public string[] RemainingArgs { get; private set; }
        public string[] UnprocessedArgs { get; private set; }

        public KubeConnectMode Action { get; set; }

        public List<EnvVarMapping> EnvVars { get; } = new List<EnvVarMapping>();

        public class EnvVarMapping
        {
            public EnvVarMapping(string map)
            {
                var span = map.AsSpan();

                if (span[0] == '-' || span[0] == '+')
                {
                    if (span[0] == '-')
                    {
                        Mode = EnvVarMappingMode.Remove;
                    }
                    else
                    {
                        Mode = EnvVarMappingMode.Append;
                    }
                    span = span.Slice(1);
                }
                var idx = span.IndexOf('=');
                if (idx >= 0)
                {
                    Value = new string(span.Slice(idx + 1));
                    span = span.Slice(0, idx);
                }

                Name = new string(span);
            }

            public EnvVarMappingMode Mode { get; set; } = EnvVarMappingMode.Replace;

            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;

            public enum EnvVarMappingMode
            {
                Remove,
                Append,
                Replace
            }
        }

        public class Mapping
        {
            public Mapping(string map)
            {
                var parts = map.Split(new[] { ':' }, 2);
                ServiceName = parts[0];
                Address = IPAddress.Parse(parts[1]);
            }

            public string ServiceName { get; set; } = string.Empty;

            public IPAddress Address { get; set; } = IPAddress.Loopback;
        }
        public class BridgeMapping
        {
            public BridgeMapping(string map)
            {
                var parts = map.Split(new[] { ':' }, 2);
                ServiceName = parts[0];
                var idx = ServiceName.LastIndexOf(',');
                if (idx > 0)
                {
                    this.RemotePort = int.Parse(ServiceName.AsSpan().Slice(idx + 1));
                }
                if (parts.Length > 1)
                {
                    LocalPort = int.Parse(parts[1]);
                }
                else
                {
                    LocalPort = this.RemotePort;
                }
            }

            public string ServiceName { get; set; } = string.Empty;
            public int RemotePort { get; set; } = -1;
            public int LocalPort { get; set; } = -1;
        }

        public enum KubeConnectMode
        {
            Connect,
            Bridge,
            Run
        }
    }
}
