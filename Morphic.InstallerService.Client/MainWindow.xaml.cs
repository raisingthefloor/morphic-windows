using Grpc.Core;
using Grpc.Net.Client;
using Morphic.InstallerService;
using Morphic.InstallerService.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private AsyncDuplexStreamingCall<ActionMessage, Response> _duplexStream;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _task;
        private readonly string[] _args;

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isIndeterminate;
        private double _progressValue;
        private string _description;

        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        public bool IsIndeterminate
        {
            get { return _isIndeterminate; }
            set
            {
                _isIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get { return _description; }
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _args = Environment.GetCommandLineArgs();

            _cancellationTokenSource = new CancellationTokenSource();
            _task = RunAsync(_cancellationTokenSource.Token);

            DataContext = this;

            Description = "Loading...";
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                var package = new Package();

                using var channel = GrpcChannel.ForAddress("https://localhost:5001");
                var client = new MorphicInstaller.MorphicInstallerClient(channel);

                _duplexStream = client.StartSession();

                var task = DisplayAsync(_duplexStream.ResponseStream, token);

                var action = _args[1];
                var application = _args[2];
                var arguments = new List<string>();

                if (_args.Length > 3)
                {
                    for (var i = 3; i < _args.Length; i++)
                    {
                        arguments.Add(_args[i]);
                    }
                }

                IsIndeterminate = true;

                if (action == "install")
                {
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Description = "Installing...";
                    }));

                    await Install(application, arguments.ToArray());
                }
                else if (action == "uninstall")
                {
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Description = "Uninstalling...";
                    }));

                    await Uninstall(application, arguments.ToArray());
                }

                await task;

                Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task DisplayAsync(IAsyncStreamReader<Response> stream, CancellationToken token)
        {
            try
            {
                await foreach (var response in stream.ReadAllAsync(token))
                {
                    switch (response.ResponseCase)
                    {
                        case Response.ResponseOneofCase.None:
                            break;
                        case Response.ResponseOneofCase.Log:
                            Console.WriteLine($"{response.Log.Message}");
                            break;
                        case Response.ResponseOneofCase.Progress:
                            Console.WriteLine($"{response.Progress.Percentage}%");
                            if (response.Progress.Percentage == 0d && !IsIndeterminate)
                                IsIndeterminate = true;
                            else if (response.Progress.Percentage != 0d && IsIndeterminate)
                                IsIndeterminate = false;

                            ProgressValue = response.Progress.Percentage;
                            break;
                        case Response.ResponseOneofCase.Complete:
                            Console.WriteLine($"Installation complete.");
                            _cancellationTokenSource.Cancel();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.Cancelled)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                System.Console.WriteLine("Finished.");
            }
        }

        private async Task Install(string application, string[] arguments)
        {
            await _duplexStream.RequestStream.WriteAsync(new ActionMessage { Install = new InstallRequest { Application = application, Arguments = { arguments } } });
        }

        public async Task Uninstall(string application, string[] arguments)
        {
            await _duplexStream.RequestStream.WriteAsync(new ActionMessage { Uninstall = new UninstallRequest { Application = application, Arguments = { arguments } } });
        }
    }
}
