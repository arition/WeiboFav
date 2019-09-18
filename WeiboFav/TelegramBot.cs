using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;
using WeiboFav.Model;

namespace WeiboFav
{
    public class TelegramBot
    {
        public TelegramBot()
        {
            BotClient = new TelegramBotClient(Program.Config["Telegram:Token"]);
        }

        private TelegramBotClient BotClient { get; }

        public async Task SendWeibo(WeiboInfo weiboInfo)
        {
            var retryTime = 0;
            var sizeLimit = 1000L * 1000 * 10; // 10MB limit
            var widthLimit = 10000;
            var heightLimit = 10000;
            while (true)
            {
                if (retryTime > 5) break;

                var files = weiboInfo.ImgUrls.Select(t => new FileInfo(t.ImgPath))
                    .Select(t => new
                    {
                        FileInfo = t,
                        Stream = t.OpenRead()
                    })
                    .Select(t => new
                    {
                        t.FileInfo.Name,
                        t.FileInfo.Length,
                        t.Stream,
                        Image = Utils.Utils.IdentifyX(t.Stream)
                    }).OrderBy(t => t.Length).ToList();

                try
                {
                    if (string.IsNullOrEmpty(weiboInfo.Url)) return;

                    if (weiboInfo.ImgUrls.Count > 1)
                    {
                        var photoInput = files.TakeWhile(t =>
                                t.Length < sizeLimit && t.Image.Height < heightLimit && t.Image.Width < widthLimit)
                            .Select(t => new InputMediaPhoto(new InputMedia(t.Stream, t.Name))).ToList();
                        var msgs = await BotClient.SendMediaGroupAsync(photoInput,
                            new ChatId(long.Parse(Program.Config["Telegram:ChatId"])));
                        await BotClient.EditMessageCaptionAsync(msgs[0].Chat.Id, msgs[0].MessageId,
                            photoInput.Count == files.Count ? weiboInfo.Url : $"More: {weiboInfo.Url}");
                    }
                    else if (weiboInfo.ImgUrls.Count == 1)
                    {
                        if (files[0].Length < sizeLimit &&
                            files[0].Image.Height < heightLimit &&
                            files[0].Image.Width < widthLimit)
                        {
                            await BotClient.SendPhotoAsync(new ChatId(long.Parse(Program.Config["Telegram:ChatId"])),
                                new InputMedia(files[0].Stream, files[0].Name), weiboInfo.Url);
                        }
                        else
                        {
                            Log.Warning($"Single img cannot be sent, weiboId {weiboInfo.Id}");
                        }
                    }
                    else
                    {
                        await BotClient.SendTextMessageAsync(
                            new ChatId(long.Parse(Program.Config["Telegram:ChatId"])),
                            weiboInfo.Url, disableWebPagePreview: true);
                    }

                    break;
                }
                catch (Exception e)
                {
                    retryTime++;
                    Log.Logger.Fatal(e, $"Failed to send message, weiboId {weiboInfo.Id}, retry ({retryTime}/5)");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    foreach (var file in files)
                    {
                        file.Stream.Close();
                        file.Stream.Dispose();
                    }
                }
            }
        }
    }
}