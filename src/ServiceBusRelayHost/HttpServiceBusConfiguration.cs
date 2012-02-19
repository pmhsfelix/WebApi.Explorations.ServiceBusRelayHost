using System.Web.Http;
using Microsoft.ServiceBus;

namespace WebApi.Explorations.ServiceBusIntegration
{
    public class HttpServiceBusConfiguration : HttpConfiguration
    {
        private readonly string _address;

        public HttpServiceBusConfiguration(string address) :base(new HttpRouteCollection(address))
        {
            _address = address;
        }

        public string IssuerName { get; set; }
        public string IssuerSecret { get; set; }
        public string Address { get { return _address; } }

        public WebHttpRelayBinding GetBinding()
        {
            return new WebHttpRelayBinding(EndToEndWebHttpSecurityMode.None, RelayClientAuthenticationType.None);
        }

        public TransportClientEndpointBehavior GetTransportBehavior()
        {
            return new TransportClientEndpointBehavior
                                 {
                                     TokenProvider =
                                         TokenProvider.CreateSharedSecretTokenProvider(IssuerName, IssuerSecret)
                                 };
        }
    }
}