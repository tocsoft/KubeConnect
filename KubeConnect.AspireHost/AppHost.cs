using Aspire.Hosting;
using Aspire.Hosting.KubeConnect;

var builder = DistributedApplication.CreateBuilder(args);

var kubCon = builder.AddKubeConnect("kubeconnect")
    .WithShowDiscoveredServices(false);
kubCon.AddKubeConnectService("seq");// show seq;

builder.AddProject<Projects.SampleApp>("sample")
    .AsKubeConnectService("tenant-api"); // adds an annotation that enabled bridging

builder.Build().Run();
