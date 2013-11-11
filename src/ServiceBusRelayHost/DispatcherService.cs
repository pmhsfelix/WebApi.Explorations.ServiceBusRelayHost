using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;
using Microsoft.ServiceBus.Web;

namespace WebApi.Explorations.ServiceBusIntegration
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    internal class DispatcherService
    {
        private readonly HttpMessageInvoker _serverInvoker;
        private readonly HttpServiceBusConfiguration _config;

        public DispatcherService(HttpServer server, HttpServiceBusConfiguration config)
        {
            _serverInvoker = new HttpMessageInvoker(server, false);
            _config = config;
        }

        private static readonly HashSet<string> _httpContentHeaders = new HashSet<string>
                                                                 {
                                                                     "Allow",
                                                                     "Content-Encoding",
                                                                     "Content-Language",
                                                                     "Content-Length",
                                                                     "Content-Location",
                                                                     "Content-MD5",
                                                                     "Content-Range",
                                                                     "Content-Type",
                                                                     "Expires",
                                                                     "Last-Modified"
                                                                 };
        
        [WebGet(UriTemplate = "*")]
        [OperationContract(AsyncPattern = true)]
        public async Task<Message> GetAsync()
        {
            var context = WebOperationContext.Current;
            var request = MakeHttpRequestMessageFrom(context.IncomingRequest, null, _config.BufferRequestContent);
            var response = await _serverInvoker.SendAsync(request, CancellationToken.None);
            CopyHttpResponseMessageToOutgoingResponse(response, context.OutgoingResponse);
            var stream = response.Content != null ? await response.Content.ReadAsStreamAsync() : null;
            return StreamMessageHelper.CreateMessage(MessageVersion.None, "GETRESPONSE", stream ?? new MemoryStream());
        }
        
        [WebInvoke(UriTemplate = "*", Method = "*")]
        [OperationContract(AsyncPattern = true)]
        public async Task<Message> InvokeAsync(Message msg)
        {
            var context = WebOperationContext.Current;
            object value;
            Stream s = null;
            if (msg.Properties.TryGetValue("WebBodyFormatMessageProperty", out value))
            {
                var prop = value as WebBodyFormatMessageProperty;
                if (prop != null && (prop.Format == WebContentFormat.Json || prop.Format == WebContentFormat.Raw))
                {
                    s = StreamMessageHelper.GetStream(msg);
                }
            }
            else
            {
                var ms = new MemoryStream();
                using (var xw = XmlDictionaryWriter.CreateTextWriter(ms, Encoding.UTF8, false))
                {
                    msg.WriteBodyContents(xw);
                }
                ms.Seek(0, SeekOrigin.Begin);
                s = ms;
            }
            var request = MakeHttpRequestMessageFrom(context.IncomingRequest, s, _config.BufferRequestContent);
            var response = await _serverInvoker.SendAsync(request, CancellationToken.None);
            CopyHttpResponseMessageToOutgoingResponse(response, context.OutgoingResponse);
            var stream = response.Content != null ? await response.Content.ReadAsStreamAsync() : null;
            return StreamMessageHelper.CreateMessage(MessageVersion.None, "GETRESPONSE", stream ?? new MemoryStream());
        }

       private static HttpRequestMessage MakeHttpRequestMessageFrom(IncomingWebRequestContext oreq, Stream body, bool bufferBody)
        {
            var nreq = new HttpRequestMessage(new HttpMethod(oreq.Method), oreq.UriTemplateMatch.RequestUri);
            foreach (var name in oreq.Headers.AllKeys.Where(name => !_httpContentHeaders.Contains(name)))
            {
                nreq.Headers.TryAddWithoutValidation(name, oreq.Headers.Get(name));
            }
            if (body != null)
            {
                if (bufferBody)
                {
                    var ms = new MemoryStream();
                    body.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    nreq.Content = new StreamContent(ms);   
                }else{
                    nreq.Content = new StreamContent(body);
                }

                foreach (var name in oreq.Headers.AllKeys.Where(name => _httpContentHeaders.Contains(name)))
                {
                    nreq.Content.Headers.TryAddWithoutValidation(name, oreq.Headers.Get(name));
                }
            }
            return nreq;
        }

        private static void CopyHttpResponseMessageToOutgoingResponse(HttpResponseMessage response,
                                                                      OutgoingWebResponseContext outgoingResponse)
        {
            outgoingResponse.StatusCode = response.StatusCode;
            outgoingResponse.StatusDescription = response.ReasonPhrase;
            if (response.Content == null) outgoingResponse.SuppressEntityBody = true;
            foreach (var kvp in response.Headers)
            {
                foreach (var value in kvp.Value)
                {
                    outgoingResponse.Headers.Add(kvp.Key, value);
                }
            }
            if (response.Content != null)
            {
                foreach (var kvp in response.Content.Headers)
                {
                    foreach (var value in kvp.Value)
                    {
                        outgoingResponse.Headers.Add(kvp.Key, value);
                    }
                }
            }
        }
    }
}