# KubeConnect

> NOTE: This is currently only tested on Windows however the core functionality should run on any OS that can run .net core.

KubeConnect is a bulk port forwarding utility for Kubernetes. Rather than having to start up port forward sessions for each port of 
each service you wish to access instead yu can just run `KubeConnect` and exposes everything in a namespace in one hit.

KubeConnect however does more than just forwards lots of ports on lots of services onto random local ports its cleverer than that, it updates your
local hosts file so the ports exposed on the service are available locally in the same way they where available inside the cluster. Lets say you 
have 2 services `website`, and `api` both exposing port 80 what `KubeConnect` does is exposes those 2 services both on port 80 on separate local IPs 
with your `HOSTS` file updated so that `website` resolves to its forwarder and `api` to its, effectively exposes them locally in a similar style to 
how they appear inside the cluster meaning your code running local can talk to backend services without needing reconfiguration.

Additionally to exposing services we also expose ingresses, generating custom (and trusted) ssl certificates so they can be access locally without 
those annoying untrusted cert warnings you get for traditional self signed certificates.

So now you know to connect into your cluster and expose services inside it to your development machine thats great but you now want to make changes
to one of the services you have deployed. This is where bridging comes into play with simple run of `KubeConnect bridge --service service-name` come
in to play. This does a couple of major things it reaches into your cluster and disables the deployment targeted by that service, it then starts up
a new temp pod that acts as a reverse proxy inside the cluster that (with the help of a service running in `kubeconnect`) redirects the traffic to
any service running on a local port. This then is the piece that squares that circle, allowing your local dev machine act like a part of the deployment
and the cluster to be accessible to your machine effectively allowing you to switch out a deployed component to one your debugging and changing giving 
you access to all the other services without having to fake out the entire stack.

## Installing

`KubeConnect` is distributed as a [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).

You will need an up to date version of the .NET SDK installed. which can be installed from the [dotnet website](https://dotnet.microsoft.com/en-us/download)

Once you have all that its a simple as running

```powershell
dotnet tool install --global Tocsoft.KubeConnect.Tool
```

This will add the tool to your path where you can run it from anywhere just by invoking `KubeConnect`

## How to `connect` to a cluster.

1. Download the latest version from our [releases page](https://github.com/tocsoft/KubeConnect/releases). 
2. Then run `KubeConnect connect` in the terminal of your choice, accepting an UAC prompts as required (required for modifying hosts file).
3. Start accessing services.

This will connect to what ever your default cluster is configured as locally.

### How To bridge
Firstly you need to have a 2nd process running in `connect` mode.
Once its connected you can then run `KubeConnect bridge --service {service-name}`
and traffic will be redirected from the default (first) exposed port in the service definition to `localhost` so if the service is setup to
listen on http://service-name:5555 then that traffic will redirect to http://localhost:5555. however that might not always be wanted especially
if you plan on starting multiple bridges and they all use the same internal port in the cluster. In that case you can use the
`KubeConnect bridge --service {service-name}:{target-port}` syntax, this just changes the destination port for the bridge.



# Project history

This project as originally written just for the connect side as a quick solution to work around an issue in kubectl/GO kubernetes client that would
cause some panic state while port forwarding and not receive some expected payload back it returns. Also it didn't handle host file updates etc to make
the experience seamless even for those who have no idea/interest in understanding kubernetes but just want to access the code.

The bridge side came about just to close the loop without having to introduce multiple competing tools that all update hosts files in different way
introducing had to diagnose bugs due to different routing kicking in.