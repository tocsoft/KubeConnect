using System.Collections.Generic;

namespace KubeConnect
{
    public class Args
    {
        Dictionary<string, string> shotNameMap = new Dictionary<string, string>()
        {
            ["n"] = "namespace",
            ["i"] = "forward-ingresses",
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
                string option = "";
                string value = "";
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
                    case "forward-ingresses":
                        ForwardIngresses = true;
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
                    default:
                        break;
                }
            }
        }

        public string Namespace { get; set; }
        public string KubeconfigFile { get; }
        public string Context { get; }
        public List<Mapping> Mappings { get; } = new List<Mapping>();
        public bool ForwardIngresses { get; }
        public string ConsolePipeName { get; }
        public bool NoLogo { get; }
        public bool Elevated { get; }
        public bool AttachDebugger { get; }
        public bool LaunchBrowser { get; } = false;

        public class Mapping
        {
            private string v;

            public Mapping(string v)
            {
                this.v = v;
            }

            public string ServiceName { get; set; }
            public int LocalPort { get; set; }
            public int RemotePort { get; set; }
        }
    }
}
