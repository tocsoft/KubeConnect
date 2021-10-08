# KubeConnect

> NOTE: This is currently only tested on Windows however the core funcationaly should run on any OS that can run .net core.

KubeConnect is a bulk port forwarding utility for Kubernetes. Rather than having to start up port forward sessions for each port of 
each service you wish to access instead yu can just run `KubeConnect` and exposes everythign in a namespace in one hit.

KubeConnect however doe more than just forwards  lots of ports on lots of service sonto random local ports its clerer than than, it updates your
local hosts file so the posrt as exposed on the service is availible locally. Lets say you have 2 services `website`, and `api` both exposing port 
80 what `KubConnect` does is exposes those 2 servies both on port 80 on seperate local IPs with you hots file configured so that `website` resolves
to its forwarder and `api` to its, effectivly exposes them locally in a similar monor to how they are available inside the cluster meaning your code 
running local can talk to backend services without needing reconfigureation.

Addtionally to exposing services we also expose ingresses, generating custom (and trusted) ssl certificates so they can be access locally without 
those annoying untrusted cert warnings you get for traditionl self signed certificates.

## How to run in.

1. Download the latest version from our [releases page](https://github.com/tocsoft/KubeConnect/releases). 
2. Then run `> KubeConnect` in the terminal of your choice, accepting an UAC prompts as required (required for modifying hosts file).
3. Start accessing services.

