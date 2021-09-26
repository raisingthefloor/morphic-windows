using System;
using System.Threading;

namespace Morphic.InstallerService.Client
{
    public class Spinner : IDisposable
    {
        private const string Sequence = @"/-\|";

        private string _busyMessage = "";
        private int _counter = 0;
        private readonly int _delay;
        private bool _active;
        private readonly Thread _thread;

        public Spinner(int delay = 200)
        {
            _delay = delay;
            _thread = new Thread(Spin);
        }

        public void Start()
        {
            _active = true;

            Console.CursorVisible = false;
            if (!_thread.IsAlive)
            {
                _thread.Start();
            }
        }

        public void Start(string busyMessage)
        {
            _busyMessage = busyMessage;
            Start();
        }

        public void Stop()
        {
            _active = false;
            Console.CursorVisible = true;
            ClearCurrentConsoleLine();
            _busyMessage = "";
        }

        private static void ClearCurrentConsoleLine()
        {
            var currentLineCursor = Console.CursorTop;

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private void Spin()
        {
            while (_active)
            {
                Turn();
                Thread.Sleep(_delay);
            }
        }

        private void Draw(char c)
        {
            var left = Console.CursorLeft;
            var top = Console.CursorTop;

            Console.Write('[');
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(c);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(']');

            if (!string.IsNullOrEmpty(_busyMessage))
            {
                Console.Write($" {_busyMessage}");
            }

            Console.SetCursorPosition(left, top);
        }

        private void Turn()
        {
            Draw(Sequence[++_counter % Sequence.Length]);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
