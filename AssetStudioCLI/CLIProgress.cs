using System;
using AssetStudio;

namespace AssetStudioCLI
{
    class CLIProgress : IProgress, ILogger, IDisposable
    {
        //private int currentValue;
        //private string currentMessage;

        public CLIProgress()
        {
            //currentValue = 0;
            //currentMessage = string.Empty;
            Logger.Default = this;
            Progress.Default = this;
            Studio.StatusStripUpdate = (msg) => Log(LoggerEvent.Verbose, msg);
        }

        //private void Next()
        //{
        //    currentValue = 0;
        //    Console.WriteLine();
        //}

        public void Dispose()
        {
            //Next();
            Logger.Default = new DummyLogger();
            Progress.Default = new DummyProgress();
            Studio.StatusStripUpdate = m => { };
        }

        public void Log(LoggerEvent loggerEvent, string message)
        {
            //currentMessage = message;
            Console.WriteLine(message);
            //Tick();
        }

        public void Report(int value)
        {
            //currentValue = value;
            //Tick();
        }
        //public static void ClearCurrentConsoleLine()
        //{
        //    int currentLineCursor = Console.CursorTop;
        //    Console.SetCursorPosition(0, Console.CursorTop);
        //    Console.Write(new string(' ', Console.BufferWidth));
        //    Console.SetCursorPosition(0, currentLineCursor);
        //}

        //public static void RewriteLine(string message)
        //{
        //    ClearCurrentConsoleLine();
        //    Console.Write(message);
        //}

        //private void Tick()
        //{
        //    var p = currentValue.ToString().PadLeft(3);
        //    RewriteLine($"[{p}%] {currentMessage}");

        //    if (currentValue == 100)
        //        Next();
        //}
    }
}
