using Aspire.Hosting.KubeConnect;

var builder = DistributedApplication.CreateBuilder(args);

var kubCon = builder.AddKubeConnect("kubeconnect")
    .WithShowDiscoveredServices(true);

var host = builder.Build();
await host.RunAsync();

// owns and runs the kubeconnect process as a child process during startup!! hide logs!