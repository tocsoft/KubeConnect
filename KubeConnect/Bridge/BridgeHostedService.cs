using CliWrap;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.Bridge
{
    public class BridgeHostedService : IHostedService
    {
        private readonly ServiceManager serviceManager;
        private readonly ILogger<BridgeHostedService> logger;
        private readonly IKubernetes kubernetes;
        private readonly IHostApplicationLifetime applicationLifetime;
        List<SshClient> sshClients = new List<SshClient>();

        public BridgeHostedService(ServiceManager serviceManager,
            ILogger<BridgeHostedService> logger,
            IKubernetes kubernetes,
            IHostApplicationLifetime applicationLifetime)
        {
            this.serviceManager = serviceManager;
            this.logger = logger;
            this.kubernetes = kubernetes;
            this.applicationLifetime = applicationLifetime;
        }

        private Task task;

        
        private Task UpdateDeployment(V1Deployment targetDeployment)
        {
            return kubernetes.ReplaceNamespacedDeploymentAsync(targetDeployment, targetDeployment.Name(), targetDeployment.Namespace(), fieldManager: "kubeconnect:bridge");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //// 1. find and rip down ssh pods (unless we are going to retargeting)
            //// 2. scale back up all deployments that where disabled. (unless  we are retargeting)
            //// 3. start any missing ssh pods
            //// 4. scale down all replaced deployments

            //// reset is just as simple as kubeconnect without bridge configs defined

            //var bridgingServices = serviceManager.Services.Where(x => x.Bridge);
            //var namespaces = serviceManager.Services.Select(x => x.Namespace).Distinct().ToList();
            //var passwordDetails = new List<(ServiceDetails service, string podName, string username, string password)>();
            //foreach (var ns in namespaces)
            //{
            //    var results = await kubernetes.ListNamespacedDeploymentAsync(ns);
            //    foreach (var dep in results.Items)
            //    {
            //        var service = bridgingServices.Where(x => dep.MatchTemplate(x)).FirstOrDefault();
            //        if (service != null)
            //        {
            //            var deployment = dep;

            //            // scale these ones to nothing (if not already done)
            //            if (dep.Spec.Replicas != 0)
            //            {
            //                dep.Metadata.Annotations["kubeconnect.bridge/original_replicas"] = dep.Spec.Replicas.ToString();
            //                dep.Spec.Replicas = 0;
            //                await UpdateDeployment(dep);
            //            }
            //        }
            //        else
            //        {
            //            await RevertScaleDown(dep);
            //        }
            //    }

            //    var namespacedServices = bridgingServices.Where(x => x.Namespace == ns);

            //    var sshPods = (await kubernetes.ListNamespacedPodAsync(ns, labelSelector: "kubeconnect.bridge/ssh=true")).Items;
            //    //remove all the pods that match our service as we go
            //    // these are unwanted pods
            //    foreach (var pod in sshPods)
            //    {
            //        await kubernetes.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
            //    }
            //    foreach (var service in namespacedServices)
            //    {
                   

            //        var password = pod.Spec.Containers.Single().Env.Single(e => e.Name == "USER_PASSWORD").Value;
            //        //var password = "";
            //        var username = "linuxserver.io";
            //        passwordDetails.Add((service, pod.Metadata.Name, username, password));
            //    }

            //}

            //Dictionary<string, string> podIps = new Dictionary<string, string>();
            //var allRunning = false;
            //while (!allRunning)
            //{
            //    allRunning = true;
            //    foreach (var ns in namespaces)
            //    {
            //        var runningPods = (await kubernetes.ListNamespacedPodAsync(ns, labelSelector: "kubeconnect.bridge/ssh=true")).Items;
            //        // at least one pod is waiting still
            //        if (runningPods.Any(x => x.Status.Phase != "Running"))
            //        {
            //            allRunning = false;
            //            continue;
            //        }

            //        foreach (var p in runningPods)
            //        {
            //            if (p.Status.Phase != "Running")
            //            {
            //                allRunning = false;
            //                continue;
            //            }
            //            podIps[p.Name()] = p.Status.PodIP;
            //        }
            //    }
            //}
            ////TODO poll waiting for pods to be active!
            //applicationLifetime.ApplicationStarted.Register(() =>
            //{
            //    this.task = Task.Run(() =>
            //    {
            //        foreach (var (service, podname, username, password) in passwordDetails)
            //        {
            //            try
            //            {
            //                var client = new SshClient(service.AssignedAddress.ToString(), username, password);
            //                client.Connect();
            //                logger.LogInformation($"Can connect to {service.ServiceName} ssh server with `ssh {internalIp}:{username}:{password}@{service.AssignedAddress}`");
            //                sshClients.Add(client);
            //                // fixing???
            //                foreach (var mappings in service.BridgedPorts)
            //                {
            //                    //var port = new ForwardedPortRemote((uint)mappings.remotePort, "localhost", (uint)mappings.localPort);
            //                    var port = new ForwardedPortRemote(IPAddress.Any, (uint)mappings.remotePort, IPAddress.Loopback, (uint)mappings.localPort);
            //                    client.AddForwardedPort(port);
            //                    port.Start();
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                applicationLifetime.StopApplication();
            //            }
            //        }
            //    });

            //}, false);
        }

        private async Task RevertScaleDown(V1Deployment dep)
        {
            //scale these ones back up, if not replicas
            if (dep.Spec.Replicas == 0 && dep.Metadata.Annotations.TryGetValue("kubeconnect.bridge/original_replicas", out var orgReplicaCount))
            {
                if (int.TryParse(orgReplicaCount, out var target))
                {
                    dep.Spec.Replicas = target;
                    dep.Metadata.Annotations.Remove("kubeconnect.bridge/original_replicas");
                    await UpdateDeployment(dep);
                }
            }
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            //// ensure we at least tried to start
            //try
            //{
            //    await this.task;
            //}
            //catch { }
            //foreach (var client in this.sshClients)
            //{
            //    client.Disconnect();
            //    client.Dispose();
            //}
            //this.sshClients.Clear();

            var namespaces = this.serviceManager.Services.Select(X => X.Namespace).Distinct();
            foreach (var ns in namespaces)
            {
                var results = await kubernetes.ListNamespacedDeploymentAsync(ns);
                foreach (var dep in results.Items)
                {
                    await RevertScaleDown(dep);
                }
                var sshPods = kubernetes.ListNamespacedPodAsync(ns, labelSelector: "kubeconnect.bridge/ssh=true").Result.Items;

                foreach (var pod in sshPods)
                {
                    await kubernetes.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                }
            }
        }
    }

    public static class Extensions
    {
        public static bool Match(this V1ObjectMeta metadata, ServiceDetails service)
        {
            var labels = metadata.Labels;
            var selector = service.Selector;

            foreach (var sel in selector)
            {
                // label missing or value not exists
                if (!labels.TryGetValue(sel.Key, out var val) || val != sel.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool MatchTemplate(this V1Deployment deployment, ServiceDetails service)
            => Match(deployment.Spec.Template.Metadata, service);

        public static bool Match(this  V1Pod pod, ServiceDetails service)
            => Match(pod.Metadata, service);
    }
}
