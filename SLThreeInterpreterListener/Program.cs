using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SLThreeInterpreterListener
{
    public class InvokeRequest
    {
        public string Code { get; set; }
    }

    public class InvokeResponse
    {
        public object Result { get; set; }
    }

    public class Program
    {
        /// <summary>
        /// For example: 8080 5 3600
        /// </summary>
        /// <param name="args">PORT TIMEOUT TTL</param>
        static async Task Main(string[] args)
        {
            var server = new HttpServer(new[] { $"http://localhost:{args[0]}/" });
            var timeout = TimeSpan.FromSeconds(int.Parse(args[1]));
            var ttl = TimeSpan.FromSeconds(int.Parse(args[2]));
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < ttl) Thread.Sleep(5000);
                Environment.Exit(0);
            });

            var slt_context = new SLThree.ExecutionContext();
            var parser = new SLThree.Language.Parser();

            server.RegisterEndpoint("POST", "/invoke", async (context) =>
            {
                var request = await HttpServer.ReadJsonAsync<InvokeRequest>(context);


                var result = default(object);
                var success = false;

                var task = new Task(() =>
                {
                    var sw = Stopwatch.StartNew();
                    while (!success && sw.Elapsed < timeout) Thread.Sleep(50);
                    sw.Stop();
                    if (!success) Environment.Exit(0);
                });
                task.Start();

                await Task.WhenAny(task, Task.Run(() =>
                {
                    try
                    {
                        SLThree.ExecutionContext.IExecutable obj;
                        try
                        {
                            obj = parser.ParseExpression(request.Code);
                        }
                        catch
                        {
                            obj = parser.ParseScript(request.Code);
                        }

                        try
                        {
                            result = obj.GetValue(slt_context);
                        }
                        catch (Exception e)
                        {
                            result = e.ToString();
                        }
                        finally
                        {
                            success = true;
                        }
                    }
                    catch (Exception e)
                    {
                        result = e.ToString();
                        success = true;
                    }
                }));

                var response = new InvokeResponse() { Result = result };
                await HttpServer.WriteJsonAsync(context, response);
            });

            var serverTask = server.StartAsync();
            await serverTask;

            server.Stop();
        }
    }
}
