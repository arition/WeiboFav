﻿using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;

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
                .WriteTo.File("logs/log-{Date}.txt", fileSizeLimitBytes: 1024 * 1024, retainedFileCountLimit: 5, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                var weiboScrape = new WeiboFavScrape();
                var telegramBot = new TelegramBot();
                weiboScrape.WeiboReceived += async (sender, e) => await telegramBot.SendWeibo(e.WeiboInfo);
                weiboScrape.VerifyRequested += async (sender, e) => await telegramBot.SendVerifyCode(e.VerifyImg);
                telegramBot.OnMessage += (sender, e) => weiboScrape.Code = e;
                weiboScrape.StartScrape();
            }
            catch (Exception e)
            {
                Log.Logger.Fatal(e, "Unknown error");
                Environment.Exit(-1);
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