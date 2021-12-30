using Grpc.Core;
using IoDCLI;
using IoDCLI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.InstallerService.Services
{
    public class InstallerGrpcService : MorphicInstaller.MorphicInstallerBase
    {
        private readonly ILogger<InstallerGrpcService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private event EventHandler<ProgressEventArgs>? Progress;
        //private event EventHandler<EventArgs>? Log;
        private event EventHandler? Complete;

        public InstallerGrpcService(ILogger<InstallerGrpcService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public override async Task StartSession(IAsyncStreamReader<ActionMessage> requestStream, IServerStreamWriter<Response> responseStream, ServerCallContext context)
        {

            Progress += async (sender, args) => await WriteProgressAsync(responseStream, args.Value);
            //Log += async (sender, args) => await WriteLogAsync(responseStream, string.Empty);
            Complete += async (sender, args) => await WriteCompleteAsync(responseStream);

            var actionsTask = HandleActions(requestStream, context.CancellationToken);

            _logger.LogInformation("Session started.");
            await AwaitCancellation(context.CancellationToken);

            try { await actionsTask; } catch { /* Ignored */ }

            _logger.LogInformation("Session finished.");
        }

        private async Task HandleActions(IAsyncStreamReader<ActionMessage> requestStream, CancellationToken token)
        {
            await foreach (var action in requestStream.ReadAllAsync(token))
            {
                switch (action.ActionCase)
                {
                    case ActionMessage.ActionOneofCase.None:
                        _logger.LogWarning("No Action specified.");
                        break;
                    case ActionMessage.ActionOneofCase.Install:
                        await Install(action.Install.Application, action.Install.Arguments.ToArray());
                        break;
                    case ActionMessage.ActionOneofCase.Uninstall:
                        await Uninstall(action.Uninstall.Application, action.Uninstall.Arguments.ToArray());
                        break;
                    default:
                        _logger.LogWarning($"Unknown Action '{action.ActionCase}'.");
                        break;
                }
            }
        }

        private static Task AwaitCancellation(CancellationToken token)
        {
            var completion = new TaskCompletionSource<object>();
            token.Register(() => completion.SetResult(null));

            return completion.Task;
        }

        private bool LoadUserProfile(WindowsIdentity user)
        {
            var profileInfo = new WindowsIdentityHelper.ProfileInfo();
            profileInfo.dwSize = Marshal.SizeOf(profileInfo);
            profileInfo.lpUserName = user.Name;
            profileInfo.dwFlags = 1;
            return WindowsIdentityHelper.LoadUserProfile(user.Token, ref profileInfo);
        }

        public async Task Install(string application, string[] arguments)
        {
            _logger.LogInformation($"Install called for application '{application}'.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                LoadUserProfile(user);

                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    //await service.InstallJaws(arguments, (sender, args) => HandleProgress(args));
                    Progress?.Invoke(this, new ProgressEventArgs(54d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(67d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(72d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(78d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(83d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(91d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(96d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(99d));
                    await Task.Delay(1000);
                    Progress?.Invoke(this, new ProgressEventArgs(100d));

                    Complete?.Invoke(this, new EventArgs());
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }

        public async Task Uninstall(string application, string[] arguments)
        {
            _logger.LogInformation($"Uninstall called for application '{application}'.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                LoadUserProfile(user);

                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    //await service.UninstallJaws(arguments);
                    Progress?.Invoke(this, new ProgressEventArgs(75d));
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }

        private void HandleProgress(ProgressEventArgs progressEventArgs)
        {
            _logger.LogInformation($"Progress: {progressEventArgs.Value}%");

            Progress?.Invoke(this, progressEventArgs);
        }

        private void HandleComplete()
        {
            _logger.LogInformation($"Installation complete.");

            Complete?.Invoke(this, new EventArgs());
        }

        //private void HandleLog(string message)
        //{
        //    _logger.LogInformation($"Log: {message}");

        //    Log?.Invoke(this, new EventArgs());
        //}

        private async Task WriteProgressAsync(IServerStreamWriter<Response> stream, double percentage)
        {
            try
            {
                await stream.WriteAsync(new Response
                {
                    Progress = new ProgressMessage
                    {
                        Percentage = percentage
                    }
                });
            }
            catch (Exception e)
            {
                // Handle any errors caused by broken connection, etc.
                _logger.LogError($"Failed to write message: {e.Message}");
            }
        }

        //private async Task WriteLogAsync(IServerStreamWriter<Response> stream, string message)
        //{
        //    try
        //    {
        //        await stream.WriteAsync(new Response
        //        {
        //            Log = new LogMessage
        //            {
        //                Message = message
        //            }
        //        });
        //    }
        //    catch (Exception e)
        //    {
        //        // Handle any errors caused by broken connection, etc.
        //        _logger.LogError($"Failed to write message: {e.Message}");
        //    }
        //}

        private async Task WriteCompleteAsync(IServerStreamWriter<Response> stream)
        {
            try
            {
                await stream.WriteAsync(new Response
                {
                    Complete = new CompleteMessage()
                });
            }
            catch (Exception e)
            {
                // Handle any errors caused by broken connection, etc.
                _logger.LogError($"Failed to write message: {e.Message}");
            }
        }
    }
}
