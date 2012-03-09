using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NUnit.Framework;
using WebApi.Explorations.ServiceBusIntegration;
using WebApi.Explorations.ServiceBusRelayHost.Tests;

namespace WebApi.Explorations.ServiceBusIntegration.Tests
{
    public class RequestExecutor<TInput,TOutput>
    {
        class Request
        {
            public TInput Value {get; private set; }
            public TaskCompletionSource<TOutput> TaskCompletionSource { get; private set; } 
            public Request(TInput t)
            {
                Value = t;
                TaskCompletionSource = new TaskCompletionSource<TOutput>();
            } 
        }

        private readonly BlockingCollection<Request> _queue = new BlockingCollection<Request>(new ConcurrentQueue<Request>());

        public Task<TOutput> Post(TInput request)
        {
            var r = new Request(request);
            _queue.Add(r);
            return r.TaskCompletionSource.Task;
        }

        public void RunSync(Func<TInput,TOutput> f)
        {
            var wi = _queue.Take();
            var r = f(wi.Value);
            wi.TaskCompletionSource.SetResult(r);
        }
    }

    public class TestController : ApiController
    {
        static readonly RequestExecutor<HttpRequestMessage,HttpResponseMessage> _getExecutor = new RequestExecutor<HttpRequestMessage, HttpResponseMessage>();
        static readonly RequestExecutor<HttpRequestMessage, HttpResponseMessage> _postExecutor = new RequestExecutor<HttpRequestMessage, HttpResponseMessage>(); 

        public static void OnGet(Func<HttpRequestMessage, HttpResponseMessage> func)
        {
            _getExecutor.RunSync(func);
        }

        public static void OnPost(Func<HttpRequestMessage, HttpResponseMessage> func)
        {
            _postExecutor.RunSync(func);
        }

        public Task<HttpResponseMessage> Get()
        {
            return _getExecutor.Post(Request);
        }

        public Task<HttpResponseMessage> Post()
        {
            return _postExecutor.Post(Request);
        }
    }

    [TestFixture]
    public class IntegrationTests
    {
        private readonly string BaseAddress = ServiceBusCredentials.ServiceBusAddress;
        private readonly string Secret = ServiceBusCredentials.Secret;

        [Test]
        public void When_GET_response_content_is_received()
        {
            const string contentString = "eureka";
            var client = new HttpClient();
            var rt = client.GetAsync(BaseAddress+"test");
            TestController.OnGet(req => new HttpResponseMessage()
                                                    {
                                                        Content = new StringContent(contentString)
                                                    }
                                     );
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(contentString, response.Content.ReadAsStringAsync().Result);        
        }

        [Test]
        public void When_GET_request_headers_are_preserved()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get,BaseAddress + "Test");
            var acceptHeaders = new MediaTypeWithQualityHeaderValue[]
                                    {
                                        new MediaTypeWithQualityHeaderValue("text/plain"),
                                        new MediaTypeWithQualityHeaderValue("application/xml", 0.13)
                                    };
            foreach(var h in acceptHeaders){ request.Headers.Accept.Add(h);}
            request.Headers.Add("X-CustomHeader", "value1");
            request.Headers.Add("X-CustomHeader", "value2");
            var rt = client.SendAsync(request);
            TestController.OnGet(req =>
                                     {
                                         foreach (var h in acceptHeaders)
                                         {
                                             Assert.True(req.Headers.Accept.Contains(h));
                                         }
                                         var customHeader = req.Headers.First(kvp => kvp.Key == "X-CustomHeader");
                                         Assert.NotNull(customHeader);
                                         Assert.True(customHeader.Value.Contains("value1"));
                                         Assert.True(customHeader.Value.Contains("value2"));
                                         return new HttpResponseMessage();
                                     });
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void When_POST_request_headers_are_preserved()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, BaseAddress + "Test");
            var acceptHeaders = new MediaTypeWithQualityHeaderValue[]
                                    {
                                        new MediaTypeWithQualityHeaderValue("text/plain"),
                                        new MediaTypeWithQualityHeaderValue("application/xml", 0.13)
                                    };
            foreach (var h in acceptHeaders) { request.Headers.Accept.Add(h); }
            request.Headers.Add("X-CustomHeader", "value1");
            request.Headers.Add("X-CustomHeader", "value2");
            request.Content = new StringContent("some content");
            request.Content.Headers.ContentLanguage.Add("en-gb");
            request.Content.Headers.ContentLanguage.Add("en-us");
            var rt = client.SendAsync(request);
            TestController.OnPost(req =>
            {
                foreach (var h in acceptHeaders)
                {
                    Assert.True(req.Headers.Accept.Contains(h));
                }
                var customHeader = req.Headers.First(kvp => kvp.Key == "X-CustomHeader");
                Assert.NotNull(customHeader);
                Assert.True(customHeader.Value.Contains("value1"));
                Assert.True(customHeader.Value.Contains("value2"));
                Assert.True(req.Content.Headers.ContentLanguage.Contains("en-gb"));
                Assert.True(req.Content.Headers.ContentLanguage.Contains("en-us"));

                return new HttpResponseMessage();
            });
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void When_POST_request_content_is_preserved()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, BaseAddress + "Test");
            var values = new Dictionary<string, string>()
                             {
                                 {"key1","value1"},
                                 {"key2","value2"},
                                 {"key3","value3"},
                                 {"key4","value4"},

                             };
            request.Content = new FormUrlEncodedContent(values);

            var rt = client.SendAsync(request);
            TestController.OnPost(req =>
            {
                var cont = req.Content.ReadAsAsync<JsonValue>().Result;
                foreach (var p in values)
                {
                    Assert.AreEqual(p.Value, cont[p.Key].ReadAs<string>());
                }
                return new HttpResponseMessage();
            }
            );
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void When_GET_response_headers_are_preserved()
        {
            const string contentString = "eureka";
            var client = new HttpClient();
            var rt = client.GetAsync(BaseAddress+"test");
            TestController.OnGet(req =>
            {
                var resp = new HttpResponseMessage()
                               {
                                   Content = new StringContent(contentString)
                               };

                resp.Content.Headers.ContentLanguage.Add("pt");
                resp.Content.Headers.ContentLanguage.Add("gr");

                resp.Headers.Add("X-CustomHeader", "value1");
                resp.Headers.Add("X-CustomHeader", "value2");
                resp.Headers.ETag = new EntityTagHeaderValue("\"12345678\"");
                
                return resp;
            });
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            Assert.True(response.Content.Headers.ContentLanguage.Contains("pt"));
            Assert.True(response.Content.Headers.ContentLanguage.Contains("gr"));

            Assert.NotNull(response.Headers.ETag);
            var customHeader = response.Headers.First(kvp => kvp.Key == "X-CustomHeader");
            Assert.NotNull(customHeader);
            Assert.True(customHeader.Value.Contains("value1,value2"));
        }

        [Test]
        public void When_POST_request_and_response_content_is_preserved()
        {
            const string contentString = "eureka";
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, BaseAddress + "test")
                              {
                                  Content = new StringContent(contentString)
                              };
            var rt = client.SendAsync(request);
            TestController.OnPost(req =>
                                      {
                                          var s = req.Content.ReadAsStringAsync().Result;
                                          Assert.AreEqual(contentString, s);
                                          return new HttpResponseMessage()
                                                     {
                                                         Content = new StringContent(s.ToUpper())
                                                     };
                                      });
            var response = rt.Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(contentString.ToUpper(), response.Content.ReadAsStringAsync().Result);        
        }

        [Test]
        public void can_handle_responses_with_no_content()
        {
            var client = new HttpClient();
            var t = client.GetAsync(BaseAddress + "test");
            TestController.OnGet( req => new HttpResponseMessage(HttpStatusCode.NoContent));
            var resp = t.Result;
            Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
        }

        private HttpServiceBusServer _server;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var config = new HttpServiceBusConfiguration(BaseAddress)
            {
                IssuerName = "owner",
                IssuerSecret = Secret,
                BufferRequestContent = true,
            };
            config.Routes.MapHttpRoute("default", "{controller}/{id}", new { id = RouteParameter.Optional });
            _server = new HttpServiceBusServer(config);
            _server.OpenAsync().Wait();     
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _server.CloseAsync().Wait();
        }
    }
}
