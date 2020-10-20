using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using Serilog;
using WeiboFav.Model;
using WeiboFav.Utils;

namespace WeiboFav
{
    internal class WeiboFavScrape
    {
        private HttpClient HttpClient { get; } = new HttpClient();
        private Regex ImgUrlRegex { get; } = new Regex(@"(?<=\d%2F).+?\.(jpg|gif|png|webp)", RegexOptions.Compiled);
        private Regex ImgUrlRegexNew { get; } = new Regex(@"(?<=pic_ids=)[,\d\w]+", RegexOptions.Compiled);
        private Regex FileNameRegex { get; } = new Regex(@"[^\/]+(?=\/$|$)", RegexOptions.Compiled);
        public string Code { private get; set; }

        public event EventHandler<WeiboEventArgs> WeiboReceived;
        public event EventHandler<VerifyEventArgs> VerifyRequested;

        public async void StartScrape()
        {
            var userDirPath = new DirectoryInfo(Program.Config["Chrome:UserDirPath"]);
            if (!userDirPath.Exists) userDirPath.Create();

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var options = new LaunchOptions();

            if (!string.IsNullOrWhiteSpace(Program.Config["Chrome:Headless"]) &&
                (Program.Config["Chrome:Headless"] == "True" || Program.Config["Chrome:Headless"] == "true"))
                options.Headless = true;
            else
                options.Headless = false;
            options.UserDataDir = userDirPath.FullName;

            using (var browser = await Puppeteer.LaunchAsync(options))
            {
                Log.Logger.Information("Browser started");
                var page = await browser.NewPageAsync();
                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width = 1920,
                    Height = 1080
                });

                var url = "https://weibo.com/fav";
                await page.GoToAsync(url);

                try
                {
                    await page.WaitForSelectorAsync(".WB_left_nav", new WaitForSelectorOptions
                    {
                        Visible = true, Timeout = 15000
                    });
                    Log.Logger.Information("User Login Success");
                }
                catch (WaitTaskTimeoutException)
                {
                    Log.Logger.Information("Try Login...");
                    await Login(page);
                }

                while (true)
                {
                    try
                    {
                        url = "https://weibo.com/fav";
                        await page.GoToAsync(url);
                        Log.Logger.Information("Checking fav Weibo...");

                        await page.WaitForSelectorAsync(".WB_feed_like", new WaitForSelectorOptions {Visible = true});
                        var weibos = await page.QuerySelectorAllAsync(".WB_feed_like");
                        var weiboInfoList = new List<WeiboInfo>();

                        foreach (var element in weibos)
                        {
                            var mid = await element.GetAttributeAsync("mid", page);
                            var ninePicTrigger = await element.QuerySelectorAllAsync(".WB_pic.li_9");
                            if (ninePicTrigger.Any())
                            {
                                await ninePicTrigger[0].ClickAsync();
                                await page.WaitForSelectorAsync($"[mid='{mid}'] .WB_expand_media_box",
                                    new WaitForSelectorOptions {Visible = true});
                            }

                            var html = await page.EvaluateFunctionAsync<string>("(el) => el.innerHTML", element);
                            var urlElement =
                                await element.QuerySelectorAsync(".WB_func li:nth-child(2) a");
                            var imgBoxesElement =
                                await element.QuerySelectorAsync(".WB_media_a[action-data]");

                            weiboInfoList.Add(new WeiboInfo
                            {
                                Id = mid,
                                RawHtml = html,
                                Url = urlElement != null
                                    ? "https://weibo.com" + await urlElement.GetAttributeAsync("href", page)
                                    : "",
                                ImgUrls = (await Task.WhenAll(
                                    PulloutImgList(imgBoxesElement != null
                                            ? await imgBoxesElement.GetAttributeAsync("action-data", page)
                                            : "")
                                        .Select(async d => new Img
                                        {
                                            ImgUrl = $"https://wx1.sinaimg.cn/large/{d}",
                                            ImgPath = await DownloadImg($"https://wx1.sinaimg.cn/large/{d}")
                                        }))).ToList()
                            });
                        }

                        using (var db = new Database())
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            weiboInfoList = weiboInfoList.Where(t => db.WeiboInfo.All(d => t.Id != d.Id)).ToList();
                            await db.WeiboInfo.AddRangeAsync(weiboInfoList);
                            await db.SaveChangesAsync();
                        }

                        File.Delete("screenshot.png");
                        await page.ScreenshotAsync("screenshot.png");
                        Log.Logger.Information($"Find {weiboInfoList.Count} new weibos");
                        if (weiboInfoList.Count > 0)
                            Log.Logger.Information("Passing weibos to telegram bot...");
                        foreach (var weiboInfo in weiboInfoList)
                            WeiboReceived?.Invoke(this, new WeiboEventArgs {WeiboInfo = weiboInfo});
                    }
                    catch (Exception e)
                    {
                        if (e is PuppeteerException)
                            //Log.Fatal(e, "Browser dead");
                            throw;

                        Log.Fatal(e, "Access Weibo failed");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        private IEnumerable<string> PulloutImgList(string html)
        {
            var listOnlyNinePic = ImgUrlRegex.Matches(html).Select(d => d.Value);
            var allPic = ImgUrlRegexNew.Match(html).Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => $"{t}.jpg");
            return listOnlyNinePic.Concat(allPic).Distinct();
        }

        private async Task Login(Page page)
        {
            await page.WaitForSelectorAsync("[name=username]", new WaitForSelectorOptions {Visible = true});

            var username = await page.QuerySelectorAsync("[name='username']");
            var password = await page.QuerySelectorAsync("[name='password']");
            var submitBtn = await page.QuerySelectorAsync("div[node-type='normal_form'] a[node-type='submitBtn']");

            await username.ClickAsync();
            await username.FocusAsync();
            // click three times to select all
            await username.ClickAsync(new ClickOptions {ClickCount = 3});
            await username.PressAsync("Backspace");
            await username.TypeAsync(Program.Config["Weibo:Username"]);

            await password.ClickAsync();
            await password.FocusAsync();
            // click three times to select all
            await password.ClickAsync(new ClickOptions {ClickCount = 3});
            await password.PressAsync("Backspace");
            await password.TypeAsync(Program.Config["Weibo:Password"]);
            await submitBtn.ClickAsync();

            while (true)
                try
                {
                    await page.WaitForSelectorAsync(".WB_left_nav", new WaitForSelectorOptions {Visible = true});
                    break;
                }
                catch (WaitTaskTimeoutException)
                {
                    if (await page.QuerySelectorAsync("#dmCheck") == null)
                    {
                        Code = "";
                        File.Delete("verify.png");
                        await page.ScreenshotAsync("verify.png");
                        Console.WriteLine("Please check verify.png for verify code");
                        using (var verifyImgStream = await page.ScreenshotStreamAsync())
                        {
                            VerifyRequested?.Invoke(this, new VerifyEventArgs {VerifyImg = verifyImgStream});
                        }

                        while (string.IsNullOrWhiteSpace(Code))
                        {
                            Log.Logger.Information("Waiting for verify code...");
                            await Task.Delay(5000);
                        }

                        try
                        {
                            await (await page.QuerySelectorAsync(".verify input")).TypeAsync(Code);
                            Code = "";
                            await submitBtn.ClickAsync();
                        }
                        catch
                        {
                            Log.Logger.Warning("Cannot submit verify code");
                        }
                    }
                    else
                    {
                        var qrCodeCheckBtn = await page.QuerySelectorAsync("#qrCodeCheck");
                        if (qrCodeCheckBtn != null)
                            await qrCodeCheckBtn.ClickAsync();
                        await Task.Delay(10000);
                        File.Delete("qrcode.png");
                        await page.ScreenshotAsync("qrcode.png");
                        using (var verifyImgStream = await page.ScreenshotStreamAsync())
                        {
                            VerifyRequested?.Invoke(this, new VerifyEventArgs {VerifyImg = verifyImgStream});
                        }

                        Log.Logger.Information("Waiting for qr code confirm...");
                    }
                }
        }

        private async Task<string> DownloadImg(string url)
        {
            var imgSavePath = new DirectoryInfo(Program.Config["ImgSavePath"]);
            if (!imgSavePath.Exists) imgSavePath.Create();
            var filePath = Path.Combine(imgSavePath.FullName, FileNameRegex.Match(url).Value);

            if (!File.Exists(filePath))
                try
                {
                    using (var httpStream = await HttpClient.GetStreamAsync(url))
                    {
                        using (var fileStream = File.Create(filePath))
                        {
                            await httpStream.CopyToAsync(fileStream);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Logger.Error(e, $"Cannot download Img: {url}");
                    return null;
                }

            return filePath;
        }

        public class WeiboEventArgs : EventArgs
        {
            public WeiboInfo WeiboInfo { get; set; }
        }

        public class VerifyEventArgs : EventArgs
        {
            public Stream VerifyImg { get; set; }
        }
    }
}