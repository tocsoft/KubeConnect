using System.Collections.Generic;
using System.Net;

namespace KubeConnect
{
    public class Args
    {
        Dictionary<string, string> shotNameMap = new Dictionary<string, string>()
        {
            ["n"] = "namespace",
            ["l"] = "launch",
            ["x"] = "context",
            ["m"] = "map",
        };

        List<string> optionsWithArguments = new List<string>()
            {
                "namespace",
                "elevated-command",
                "context",
                "map",
                "kubeconfig"
            };

        public Args(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i]?.ToLower();
                if (a == null)
                {
                    continue;
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

                // clean up argNext is required???
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
                    case "forward-mapped-only":
                        AllServices = false;
                        break;
                    default:
                        break;
                }
            }
        }

        public string? Namespace { get; set; }
        public string? KubeconfigFile { get; }
        public string? Context { get; }
        public List<Mapping> Mappings { get; } = new List<Mapping>();
        public string? ConsolePipeName { get; }
        public bool NoLogo { get; }
        public bool Elevated { get; }
        public bool AttachDebugger { get; }
        public bool LaunchBrowser { get; } = false;
        public bool UpdateHosts { get; } = true;
        public bool UseSsl { get; } = true;
        public bool AllServices { get; } = true;

        public bool RequireAdmin => UpdateHosts || UseSsl;

        public class Mapping
        {
            private string v;

            public Mapping(string map)
            {
                var parts = map.Split(new[] { ':' }, 2);
                ServiceName = parts[0];
                Address = IPAddress.Parse(parts[1]);
            }

            public string ServiceName { get; set; } = string.Empty;

            public IPAddress Address { get; set; } = IPAddress.Loopback;
        }
    }
}
