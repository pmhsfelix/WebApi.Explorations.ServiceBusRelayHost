# ASP.NET Web API Host using Azure Service Bus Relaying

## Introduction

This project aims to provide a `HttpServiceBusHost` class for hosting ASP.NET Web API using the Azure Service Bus relaying functionality.

Usage example:

```csharp
var config = new HttpServiceBusConfiguration(ServiceBusCredentials.ServiceBusAddress)
    {
        IssuerName = "owner",
        IssuerSecret = ServiceBusCredentials.Secret
    };
config.Routes.MapHttpRoute("default", "{controller}/{id}", new { id = RouteParameter.Optional });
var server = new HttpServiceBusServer(config);
server.OpenAsync().Wait();
Console.WriteLine("Server is opened at {0}", config.Address);
Console.ReadKey();
server.CloseAsync().Wait();
```


## Build

1. The `ServiceBusRelayHost.Tests` and `ServiceBusRelayHost.Demo.Screen` projects are both missing a `SecretCredentials.cs` file.
These file contain the Service Bus credentials, with the following structure, and are never committed into the repository.

```csharp
namespace WebApi.Explorations.ServiceBusRelayHost.Tests
{
    public class ServiceBusCredentials
    {
        public static readonly string ServiceBusAddress = "https://???????.servicebus.windows.net/webapi/";
        public static readonly string Secret = "????";
    }
}
``` 

Please create these files locally, using an adequate service bus address and secret.



