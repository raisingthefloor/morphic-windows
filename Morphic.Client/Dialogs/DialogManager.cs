namespace Morphic.Client.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Extensions.DependencyInjection;

    public class DialogManager
    {
        private readonly Dictionary<string, TaskCompletionSource<bool>>
            dialogs = new Dictionary<string, TaskCompletionSource<bool>>();

        /// <summary>
        /// Create a new dialog, or get an existing one.
        /// </summary>
        /// <param name="windowType">Type of window</param>
        /// <param name="taskSource">
        /// Task completion source for the dialog. The <c>AsyncState</c> property of the Task is set to the Window.
        /// </param>
        /// <returns>true if the dialog has been created.</returns>
        public bool GetDialog(Type windowType, Dictionary<string, object?> args, out TaskCompletionSource<bool> taskSource)
        {
            string name = windowType.Name;

            TaskCompletionSource<bool>? ts;

            bool isNew = !this.dialogs.TryGetValue(name, out ts);
            if (isNew)
            {
                Window window = (Window)App.Current.ServiceProvider.GetRequiredService(windowType);

                // assign the window properties from args
                var windowWithArgs = window as MorphicWindowWithArgs;
                if (windowWithArgs is not null)
                {
                    windowWithArgs.SetArguments(args);
                }

                window.Closed += this.WindowClosed;
                taskSource = new TaskCompletionSource<bool>(window);
            }
            else
            {
                taskSource = ts!;
            }

            return isNew;
        }

        /// <summary>Window close event handler - de-references the window and completes its task.</summary>
        private void WindowClosed(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                if (this.dialogs.Remove(window.GetType().Name, out TaskCompletionSource<bool>? taskSource))
                {
                    taskSource.SetResult(window.DialogResult ?? default);
                }
            }
        }

        /// <summary>Opens a window of the given type.</summary>
        /// <typeparam name="T">The type of window</typeparam>
        /// <returns>A Task that completes when the window is closed.</returns>
        public async Task<bool> OpenDialogAsync<T>(Dictionary<string, object?> args)
            where T : Window
        {
            return await this.OpenDialogAsync(args, out T _);
        }

        /// <summary>Opens a window of the given type.</summary>
        /// <param name="windowType">Type of the window.</param>
        /// <returns>A Task that completes when the window is closed.</returns>
        public async Task<bool> OpenDialogAsync(Type windowType, Dictionary<string, object?> args)
        {
            if (!typeof(Window).IsAssignableFrom(windowType))
            {
                throw new ArgumentException("Given type should be a Window sub-class.", nameof(windowType));
            }

            return await this.OpenDialogAsync(windowType, args, out Window _);
        }

        /// <summary>Opens a window of the given type.</summary>
        /// <param name="window">The window.</param>
        /// <typeparam name="T">The type of window</typeparam>
        /// <returns>A Task that completes when the window is closed.</returns>
        public Task<bool> OpenDialogAsync<T>(Dictionary<string, object?> args, out T window)
            where T : Window
        {
            Task<bool> task = this.OpenDialogAsync(typeof(T), args, out Window w);
            window = (T)w;
            return task;
        }

        /// <summary>Opens a window of the given type.</summary>
        /// <param name="window">The window.</param>
        /// <param name="windowType">Type of the window.</param>
        /// <returns>A Task that completes when the window is closed.</returns>
        public Task<bool> OpenDialogAsync(Type windowType, Dictionary<string, object?> args, out Window window)
        {
            bool isNew = this.GetDialog(windowType, args, out TaskCompletionSource<bool> taskSource);

            window = (Window)taskSource.Task.AsyncState!;

            if (isNew)
            {
                window.Show();
            }

            window.Activate();
            return taskSource.Task;
        }
    }
}
