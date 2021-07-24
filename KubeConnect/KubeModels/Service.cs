using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KubeConnect.KubeModels
{
    public class Service
    {
        public string ApiVersion { get; set; }
        public Metadata Metadata { get; set; }
        public ServiceSpec Spec { get; set; }
    }

    public class Metadata
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public Dictionary<string, string> Annotations { get; set; }
        public Dictionary<string, string> Labels { get; set; }
    }

    public class ServiceSpec
    {
        public ServiceSpecPort[] Ports { get; set; }
        public Dictionary<string,string> Selector  { get; set; }
    }

    public class ServiceSpecPort
    {
        public int Port { get; set; }
    }
}
