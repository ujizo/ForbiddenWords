using System;
using System.Threading;
using System.Windows.Forms;

namespace ForbiddenWords;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using Mutex mutex = new Mutex(false, "ForbiddenWordsApp");

        if (!mutex.WaitOne(0))
            return;

        try
        {
            if (args.Length >= 2)
            {
                ConsoleMode.Run(args);
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}

public static class ConsoleMode
{
    public static void Run(string[] args)
    {
        try
        {
            string source = args[0];
            string destination = args[1];

            string[] words = args.Length > 2
                ? args.Skip(2).ToArray()
                : File.ReadAllLines("words.txt");

            var pause = new ManualResetEventSlim(true);

            var result = Scanner.RunAsync(
                source,
                destination,
                words,
                null,
                pause,
                CancellationToken.None
            ).GetAwaiter().GetResult();

            Console.WriteLine("Проверено: " + result.Checked);
            Console.WriteLine("Найдено: " + result.Files.Count);
            Console.WriteLine("Отчёт: " + Path.Combine(destination, "Report.txt"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}