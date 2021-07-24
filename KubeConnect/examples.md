
```
> kubeconnect -m service-name,5555:5555 -m
```

```
{
	"namespace": "",
	"context":"context-name",
	"config-file" : "path to config file",
	"forward-ingresses":true, // find and forward the ingress controller and add dns entries for the mappings
	"mapping": {
		"service1-name": {
			"local-port":"remote-port"
		}
	}
}
```