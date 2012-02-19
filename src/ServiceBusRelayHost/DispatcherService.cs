using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.ServiceBus.Web;

namespace WebApi.Explorations.ServiceBusIntegration
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    internal class DispatcherService
    {
        private readonly HttpServer _server;

        public DispatcherService(HttpServer server)
        {
            _server = server;
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
        public IAsyncResult BeginGet(AsyncCallback callback, object state)
        {
            var context = WebOperationContext.Current;
            return DispatchToHttpServer(context.IncomingRequest, null, context.OutgoingResponse, callback, state);
        }

        public Message EndGet(IAsyncResult ar)
        {
            var t = ar as Task<Stream>;
            var stream = t.Result;
            return StreamMessageHelper.CreateMessage(MessageVersion.None, "GETRESPONSE", stream ?? new MemoryStream());
        }

        [WebInvoke(UriTemplate = "*", Method = "*")]
        [OperationContract(AsyncPattern = true)]
        public IAsyncResult BeginInvoke(Stream s, AsyncCallback callback, object state)
        {
            var context = WebOperationContext.Current;
            return DispatchToHttpServer(context.IncomingRequest, s, context.OutgoingResponse, callback, state);
        }

        public Message EndInvoke(IAsyncResult ar)
        {
            var t = ar as Task<Stream>;
            var stream = t.Result;
            return StreamMessageHelper.CreateMessage(MessageVersion.None, "GETRESPONSE", stream ?? new MemoryStream());
        }

        private IAsyncResult DispatchToHttpServer(
            IncomingWebRequestContext incomingRequest, 
            Stream body,
            OutgoingWebResponseContext outgoingResponse, 
            AsyncCallback callback, 
            object state)
        {
            var request = MakeHttpRequestMessageFrom(incomingRequest, body);
            var tcs = new TaskCompletionSource<Stream>(state);
            _server.SubmitRequestAsync(request, new CancellationToken())
                .ContinueWith(t =>
                {
                    var response = t.Result;
                    CopyHttpResponseMessageToOutgoingResponse(response, outgoingResponse);
                    Action<Task<Stream>> complete = (t2) =>
                    {
                        if (t2.IsFaulted)
                            tcs.TrySetException(
                                t2.Exception.InnerExceptions);
                        else if (t2.IsCanceled) tcs.TrySetCanceled();
                        else tcs.TrySetResult(t2.Result);

                        if (callback != null) callback(tcs.Task);
                    };

                    if (response.Content == null)
                    {
                        tcs.TrySetResult(null);
                        if (callback != null) callback(tcs.Task);
                    }
                    else
                    {
                        response.Content.ReadAsStreamAsync()
                            .ContinueWith(complete);
                    }
                });
            return tcs.Task;
        }
        
        private static HttpRequestMessage MakeHttpRequestMessageFrom(IncomingWebRequestContext oreq, Stream body)
        {
            var nreq = new HttpRequestMessage(new HttpMethod(oreq.Method), oreq.UriTemplateMatch.RequestUri);
            foreach (var name in oreq.Headers.AllKeys.Where(name => !_httpContentHeaders.Contains(name)))
            {
                nreq.Headers.AddWithoutValidation (name, oreq.Headers.Get(name).Split(',').Select(s => s.Trim()));
            }
            if (body != null)
            {
                nreq.Content = new StreamContent(body);
                foreach (var name in oreq.Headers.AllKeys.Where(name => _httpContentHeaders.Contains(name)))
                {
                    nreq.Content.Headers.AddWithoutValidation(name, oreq.Headers.Get(name).Split(',').Select(s => s.Trim()));
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