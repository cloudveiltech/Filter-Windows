
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using HttpMultipartParser;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageFilter
{
    class Server
    {
        public const int PORT = 8994;

        const string MODEL_PATH = "./model.onnx";

        static InferenceSession session;
        static Dictionary<String, bool> cache = new Dictionary<String, bool>();

        private WebServer server;
        public bool Start(int port)
        {
            server = new WebServer(o => o
                  .WithUrlPrefix($"http://127.0.0.1:{port}")
                  .WithMode(HttpListenerMode.EmbedIO))
              .WithWebApi("/api", m => m
                  .WithController<UploadController>());


            var sessionOptions = new SessionOptions();
            sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;
            sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;

            session = new InferenceSession(MODEL_PATH, sessionOptions);
            server.RunAsync();
            return true;
        }

        public bool Stop()
        {
            server.Dispose();
            return true;
        }

        public class UploadController : WebApiController
        {
            [Route(HttpVerbs.Get, "/hi")]
            public string SayHi()
            {              
                return "Hi from image checker web server";
            }

            [Route(HttpVerbs.Post, "/check")]
            public async Task<Dictionary<string, bool>> UploadFile()
            {
                var resultDict = new Dictionary<String, bool>();
                var parser = await MultipartFormDataParser.ParseAsync(Request.InputStream);

                var url = "";
                if (parser.HasParameter("url"))
                {                    
                    url = parser.GetParameterValue("url");
                    Console.WriteLine("URL IS " + url);
                    bool blocked;
                    if (cache.TryGetValue(url, out blocked))
                    {
                        Console.WriteLine("URL cached " + blocked);
                        resultDict.Add("cached", true);
                        resultDict.Add("allowed", blocked);
                        return resultDict;
                    }
                }

                var imageFile = parser.Files.First();
                var imageData = imageFile.Data;

                bool res = await isImageAllowed(imageData);
                resultDict.Add("allowed", res);
                Console.WriteLine("URL processed " + res);
                if (url != "")
                {
                    cache.Add(url, res);
                }
                return resultDict;
            }

            private async Task<bool> isImageAllowed(Stream imageData)
            {
                var image = await Image.LoadAsync<Rgb24>(imageData);
                image.Mutate(x => x.Resize(256, 256));

                var inputTensor = PreprocessImage(image);
                var input = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input_1", inputTensor) };

                // Create an InferenceSession from the Model Path.
                var outputResults = session.Run(input);
                if (outputResults != null && outputResults.Count > 0) {
                    var output = (DenseTensor<float>)outputResults.ToList()[0].Value;

                    var isSafe = output.GetValue(0) < output.GetValue(1);
                    return isSafe;
                }
                return false;
            }

            private Tensor<float> PreprocessImage(Image<Rgb24> image)
            {
                Tensor<float> input = new DenseTensor<float>(new[] { 1, 3, 256, 256 });
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgb24> pixelSpan = image.GetPixelRowSpan(y);
                    for (int x = 0; x < image.Width; x++)
                    {
                        input[0, 0, y, x] = pixelSpan[x].R / 255f;
                        input[0, 1, y, x] = pixelSpan[x].G / 255f;
                        input[0, 2, y, x] = pixelSpan[x].B / 255f;
                    }
                }

                return input;
            }
        }
    }
}
