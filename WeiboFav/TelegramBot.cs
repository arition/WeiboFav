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
            BotClient.OnMessage += (sender, e) =>
            {
                if (e.Message.Chat.Id.ToString() == Program.Config["Telegram:AdminChatId"])
                    OnMessage?.Invoke(this, e.Message.Text);
            };
        }

        private TelegramBotClient BotClient { get; }

        public event EventHandler<string> OnMessage;

        public async Task SendWeibo(WeiboInfo weiboInfo)
        {
            var retryTime = 0;
            //var sizeLimit = 1000L * 1000 * 10; // 10MB limit
            //var widthLimit = 10000;
            //var heightLimit = 10000;
            while (true)
            {
                if (retryTime > 5) break;

                var files = weiboInfo.ImgUrls
                    .Where(t => !string.IsNullOrEmpty(t.ImgPath))
                    .Select(t => new FileInfo(t.ImgPath))
                    .Select(t => new
                    {
                        FileInfo = t,
                        Stream = Utils.Utils.ResaveImage(t.OpenRead())
                    })
                    .Select(t => new
                    {
                        t.FileInfo.Name,
                        t.FileInfo.Length,
                        Image = Utils.Utils.IdentifyX(t.Stream),
                        t.Stream
                    }).OrderBy(t => t.Length)
                    /*.TakeWhile(t => t.Length < sizeLimit &&
                                    t.Image.Height < heightLimit &&
                                    t.Image.Width < widthLimit)*/
                    .ToList();

                try
                {
                    if (string.IsNullOrEmpty(weiboInfo.Url)) return;

                    if (files.Count > 1)
                    {
                        /*var totalSize = 0L;
                        (totalSize+=t.Length) < sizeLimit && */
                        var photoInput = files.Select(t => new InputMediaPhoto(new InputMedia(t.Stream, t.Name)))
                            .ToList();
                        photoInput[0].Caption =
                            photoInput.Count == files.Count ? weiboInfo.Url : $"More: {weiboInfo.Url}";
                        await BotClient.SendMediaGroupAsync(photoInput,
                            new ChatId(long.Parse(Program.Config["Telegram:ChatId"])));
                    }
                    else if (files.Count == 1)
                    {
                        await BotClient.SendPhotoAsync(new ChatId(long.Parse(Program.Config["Telegram:ChatId"])),
                            new InputMedia(files[0].Stream, files[0].Name), weiboInfo.Url);
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

        public async Task SendVerifyCode(Stream img)
        {
            await BotClient.SendPhotoAsync(Program.Config["Telegram:AdminChatId"], new InputMedia(img, "verify.png"));
        }
    }
}