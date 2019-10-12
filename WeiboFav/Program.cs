using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.Memory;

namespace WeiboFav
{
    internal class Program
    {
        public static IConfigurationRoot Config { get; private set; }

        private static void Main(string[] args)
        {
            var configFile = new FileInfo("config.json");
            if (!configFile.Exists)
                using (var writer = configFile.CreateText())
                {
                    writer.Write("{}");
                }

            var configBuilder = new ConfigurationBuilder();
            Config = configBuilder
                .AddJsonFile("config.json")
                .AddEnvironmentVariables("weibofav")
                .AddCommandLine(args)
                .Build();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.RollingFile("logs/log-{Date}.txt", fileSizeLimitBytes: 1024 * 1024, retainedFileCountLimit: 5)
                .CreateLogger();

            Configuration.Default.MemoryAllocator = new SimpleGcMemoryAllocator();

            try
            {
                var weiboScrape = new WeiboFavScrape();
                var telegramBot = new TelegramBot();
                weiboScrape.WeiboReceived += async (sender, e) => await telegramBot.SendWeibo(e.WeiboInfo);
                weiboScrape.StartScrape();
            }
            catch (Exception e)
            {
                Log.Logger.Fatal(e, "Unknown error");
            }


            var autoResetEvent = new AutoResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                autoResetEvent.Set();
            };
            autoResetEvent.WaitOne();
        }
    }
}