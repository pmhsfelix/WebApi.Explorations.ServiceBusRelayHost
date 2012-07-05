using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApi.Explorations.ServiceBusIntegration
{
    public class HttpServiceBusServer
    {
        private readonly HttpServer _innerServer;
        private WebServiceHost _host;
        private readonly HttpServiceBusConfiguration _config;

        public HttpServiceBusServer(HttpServiceBusConfiguration config)
        {
            _innerServer = new HttpServer(config);
            _config = config;
        }

        public Task OpenAsync()
        {
            _host = new WebServiceHost(new DispatcherService(_innerServer, _config));
            var ep = _host.AddServiceEndpoint(typeof(DispatcherService), _config.GetBinding(), _config.Address);
            ep.Behaviors.Add(_config.GetTransportBehavior());
            return Task.Factory.FromAsync(
                _host.BeginOpen,
                _host.EndOpen,
                null);
        }

        public Task CloseAsync()
        {
            return Task.Factory.FromAsync(
                _host.BeginClose,
                _host.EndClose,
                null);
        }
    }
}