using System.ServiceModel.Channels;
using System.Web.Http;
using Microsoft.ServiceBus;

namespace WebApi.Explorations.ServiceBusIntegration
{

    internal class RawContentTypeMapper : WebContentTypeMapper
    {
        public override WebContentFormat GetMessageFormatForContentType(string contentType)
        {
            return WebContentFormat.Raw;
        }
    }

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

        public bool BufferRequestContent { get; set; }
        
        public Binding GetBinding()
        {
            var b = new WebHttpRelayBinding(EndToEndWebHttpSecurityMode.None, RelayClientAuthenticationType.None);
            var elems = b.CreateBindingElements();
            var ee = elems.Find<WebMessageEncodingBindingElement>();
            ee.ContentTypeMapper = new RawContentTypeMapper();
            return new CustomBinding(elems);
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