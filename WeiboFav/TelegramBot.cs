using System;
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
        private TelegramBotClient BotClient { get; }

        public TelegramBot()
        {
            BotClient = new TelegramBotClient(Program.Config["Telegram:Token"]);
        }

        public async Task SendWeibo(WeiboInfo weiboInfo)
        {
            var retryTime = 0;
            while (true)
            {
                if (retryTime > 5) break;
                try
                {
                    if (string.IsNullOrEmpty(weiboInfo.Url)) return;

                    if (weiboInfo.ImgUrls.Count > 1)
                    {
                        var photoInput = weiboInfo.ImgUrls.Select(t => new InputMediaPhoto(t.ImgUrl)).Take(9);
                        var msgs = await BotClient.SendMediaGroupAsync(photoInput,
                            new ChatId(long.Parse(Program.Config["Telegram:ChatId"])));
                        await BotClient.EditMessageCaptionAsync(msgs[0].Chat.Id, msgs[0].MessageId,
                            weiboInfo.Url);
                    }
                    else if (weiboInfo.ImgUrls.Count == 1)
                    {
                        await BotClient.SendPhotoAsync(new ChatId(long.Parse(Program.Config["Telegram:ChatId"])),
                            new InputMedia(weiboInfo.ImgUrls[0].ImgUrl), weiboInfo.Url);
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
                    Log.Logger.Fatal(e, "Failed to send message");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    retryTime++;
                }
            }
        }
    }
}