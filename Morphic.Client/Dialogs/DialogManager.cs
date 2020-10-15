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
        /// <param name="taskSource">
        /// Task completion source for the dialog. The <c>AsyncState</c> property of the Task is set to the Window.
        /// </param>
        /// <typeparam name="T">The type of dialog.</typeparam>
        /// <returns>true if the dialog has been created.</returns>
        public bool GetDialog<T>(out TaskCompletionSource<bool> taskSource)
            where T : Window
        {
            string name = typeof(T).Name;

            TaskCompletionSource<bool>? ts;

            bool isNew = !this.dialogs.TryGetValue(name, out ts);
            if (isNew)
            {
                Window window = App.Current.ServiceProvider.GetRequiredService<T>();
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
        public Task<bool> OpenDialog<T>()
            where T : Window
        {
            return this.OpenDialog(out T _);
        }

        /// <summary>Opens a window of the given type.</summary>
        /// <param name="window">The window.</param>
        /// <typeparam name="T">The type of window</typeparam>
        /// <returns>A Task that completes when the window is closed.</returns>
        public Task<bool> OpenDialog<T>(out T window)
            where T : Window
        {
            bool isNew = this.GetDialog<T>(out TaskCompletionSource<bool> taskSource);

            window = (T)taskSource.Task.AsyncState!;

            if (isNew)
            {
                window.Show();
            }

            window.Activate();
            return taskSource.Task;
        }
    }
}
