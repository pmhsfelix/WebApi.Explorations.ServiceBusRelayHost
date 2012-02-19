using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Windows.Forms;
using WebApi.Explorations.ServiceBusIntegration;
using WebApi.Explorations.ServiceBusRelayHost.Tests;

namespace WebApi.Explorations.ServiceBusRelayHost.Demo
{
    public class ScreenCapturer
    {
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().Where(codec => codec.FormatID == format.Guid).First();
        }

        private static void GenerateImageBufferInto(Stream os)
        {
            using (var bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(
                        Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y,
                        0, 0,
                        Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

                    ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 20L);

                    bitmap.Save(os, jgpEncoder, encoderParams);
                }
            }
        }

        public static void GetEncodedBytesInto(Stream os)
        {
            GenerateImageBufferInto(os);
        }

        public static Stream GetEncodedByteStream()
        {
            var ms = new MemoryStream();
            GetEncodedBytesInto(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }

    
    public class ScreenController : ApiController
    {
        public HttpResponseMessage Get()
        {
            var content = new StreamContent(ScreenCapturer.GetEncodedByteStream());
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage()
            {
                Content = content
            };
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
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
        }
    }
}
