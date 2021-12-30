//using Grpc.Core;
//using Grpc.Net.Client;
////using JKang.IpcServiceFramework.Client;
////using Microsoft.Extensions.DependencyInjection;
//using Morphic.InstallerService.Contracts;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Morphic.InstallerService.Client
//{
//    class Program
//    {
//        private static AsyncDuplexStreamingCall<ActionMessage, Response> _duplexStream;
//        private static CancellationTokenSource _tokenSource;

//        static async Task Main(string[] args)
//        {
//            if (args.Length == 0)
//                return;

//            var package = new Package();

//            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
//            var client = new MorphicInstaller.MorphicInstallerClient(channel);

//            _duplexStream = client.StartSession();

//            _tokenSource = new CancellationTokenSource();
//            var task = DisplayAsync(_duplexStream.ResponseStream, _tokenSource.Token);

//            //var serviceProvider = new ServiceCollection()
//            //    .AddNamedPipeIpcClient<IInstallerService>("client1", pipeName: "moprhicinstaller")
//            //    .BuildServiceProvider();

//            //var clientFactory = serviceProvider.GetRequiredService<IIpcClientFactory<IInstallerService>>();

//            //var client = clientFactory.CreateClient("client1");

//            var action = args[0];
//            var application = args[1];
//            var arguments = new List<string>();

//            if (args.Length > 2)
//            {
//                for (var i = 2; i < args.Length; i++)
//                {
//                    arguments.Add(args[i]);
//                }
//            }

//            if (action == "install")
//            {
//                await ConsoleUtils.BusyIndicator("Installing please wait.", async () =>
//                {
//                    //await client.InvokeAsync(x => x.Install(application, arguments.ToArray()));
//                    await Install(application, arguments.ToArray());
//                });
//            }
//            else if (action == "uninstall")
//            {
//                await ConsoleUtils.BusyIndicator("Uninstalling please wait.", async () =>
//                {
//                    //await client.InvokeAsync(x => x.Uninstall(application, arguments.ToArray()));
//                    await Uninstall(application, arguments.ToArray());
//                });
//            }

//            Console.ReadKey();
//            _tokenSource.Cancel();
//            await task;
//        }

//        static async Task DisplayAsync(IAsyncStreamReader<Response> stream, CancellationToken token)
//        {
//            try
//            {
//                await foreach (var response in stream.ReadAllAsync(token))
//                {
//                    switch (response.ResponseCase)
//                    {
//                        case Response.ResponseOneofCase.None:
//                            break;
//                        case Response.ResponseOneofCase.Log:
//                            Console.WriteLine($"{response.Log.Message}");
//                            break;
//                        case Response.ResponseOneofCase.Progress:
//                            Console.WriteLine($"{response.Progress.Percentage}%");
//                            break;
//                        case Response.ResponseOneofCase.Complete:
//                            Console.WriteLine($"Installation complete.");
//                            _tokenSource.Cancel();
//                            break;
//                        default:
//                            break;
//                    }
//                }
//            }
//            catch (RpcException e)
//            {
//                if (e.StatusCode == StatusCode.Cancelled)
//                {
//                    return;
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                System.Console.WriteLine("Finished.");
//            }
//        }

//        private static async Task Install(string application, string[] arguments)
//        {
//            await _duplexStream.RequestStream.WriteAsync(new ActionMessage { Install = new InstallRequest { Application = application, Arguments = { arguments } } });
//        }

//        public static async Task Uninstall(string application, string[] arguments)
//        {
//            await _duplexStream.RequestStream.WriteAsync(new ActionMessage { Uninstall = new UninstallRequest { Application = application, Arguments = { arguments } } });
//        }
//    }
//}
