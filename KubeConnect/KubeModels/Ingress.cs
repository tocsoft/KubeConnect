using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KubeConnect.KubeModels
{
    public class Ingress
    {
        public string ApiVersion { get; set; }
        public Metadata Metadata { get; set; }
        public IngressSpec Spec { get; set; }
        public IngressStatus Status { get; set; }
    }
    //"status": {
    //           "loadBalancer": {
    //               "ingress": [
    //                   {
    //                       "ip": "51.143.242.171"
    //                   }
    //               ]
    //           }
    //       }
    public class IngressStatus
    {
        public IngressStatusLoadBalancer LoadBalancer { get; set; }
    }
    public class IngressStatusLoadBalancer
    {
        public IngressStatusLoadBalancerIp[] Ingress { get; set; }
    }
    public class IngressStatusLoadBalancerIp
    {
        public string IP { get; set; }
    }
    public class IngressSpec
    {
        public IngressSpecRule[] Rules { get; set; }
    }
    public class IngressSpecRule
    {
        public string host { get; set; }
    }
}
