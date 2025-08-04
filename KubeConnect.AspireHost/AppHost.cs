using Aspire.Hosting;
using Aspire.Hosting.KubeConnect;

var builder = DistributedApplication.CreateBuilder(args);

var kubCon = builder.AddKubeConnect("kubeconnect")
    .WithShowDiscoveredServices(false); // show bridges status??

kubCon.AddKubeConnectService("tenant-api");
kubCon.AddKubeConnectService("seq");// show seq;
kubCon.AddKubeConnectService("apphost");

builder.AddProject<Projects.SampleApp>("sample")
    .WithExplicitStart()
    .AsKubeConnectService("tenant-api"); // adds an annotation that enabled bridging

builder.Build().Run();
