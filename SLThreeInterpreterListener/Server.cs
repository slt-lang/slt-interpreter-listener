using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SLThreeInterpreterListener
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _routes;
        private bool _isRunning;

        public HttpServer(string[] prefixes)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException();

            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }

            _routes = new Dictionary<string, Func<HttpListenerContext, Task>>();
        }

        public void RegisterEndpoint(string method, string path, Func<HttpListenerContext, Task> handler)
        {
            string key = $"{method.ToUpper()} {path.ToLower()}";
            _routes[key] = handler;
        }

        public async Task StartAsync()
        {
            _listener.Start();
            _isRunning = true;
            Console.WriteLine("Server started...");

            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) when (!_isRunning)
                {

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            _listener.Close();
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string method = context.Request.HttpMethod;
                string path = context.Request.Url.AbsolutePath.ToLower();
                string key = $"{method} {path}";

                if (_routes.ContainsKey(key))
                {
                    await _routes[key](context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки запроса: {ex}");
                context.Response.StatusCode = 500;
                await WriteJsonAsync(context, new { error = ex.Message });
            }
        }

        public static async Task WriteJsonAsync(HttpListenerContext context, object data)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;

            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);

            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        public static async Task<T> ReadJsonAsync<T>(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                var body = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(body);
            }
        }
    }
}