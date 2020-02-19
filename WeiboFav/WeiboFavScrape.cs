using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumExtras.WaitHelpers;
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

            var options = new ChromeOptions();
#if DEBUG
            options.AddArguments($"--user-data-dir={userDirPath.FullName}");
#else
            options.AddArguments("--headless", "--disable-gpu", $"--user-data-dir={userDirPath.FullName}");
#endif

            using (var webDriver = new ChromeDriver(Program.Config["Chrome:DriverPath"], options))
            {
                webDriver.Manage().Window.Size = new Size(1920, 1080);
                Log.Logger.Information("WebDriver started");

                var url = "http://weibo.com";
                webDriver.Navigate().GoToUrl(url);

                var waitJump = new AsyncWait(TimeSpan.FromSeconds(15));
                try
                {
                    await waitJump.UntilAsync(webDriver, ExpectedConditions.UrlMatches(@"weibo.com/.+/home"));
                    Log.Logger.Information("User Login Success");
                }
                catch (TimeoutException)
                {
                    Log.Logger.Information("Try Login...");
                    await Login(webDriver);
                }

                while (true)
                {
                    try
                    {
                        url = "http://weibo.com/fav";
                        webDriver.Navigate().GoToUrl(url);
                        Log.Logger.Information("Checking fav Weibo...");

                        await waitJump.UntilAsync(webDriver,
                            ExpectedConditions.ElementIsVisible(By.CssSelector(".WB_feed_like")));
                        var weibos = webDriver.FindElements(By.CssSelector(".WB_feed_like"));
                        foreach (var element in weibos)
                        {
                            var ninePicTrigger = element.FindElements(By.CssSelector(".WB_pic.li_9"));
                            if (ninePicTrigger.Count == 0) continue;
                            ninePicTrigger[0].Click();
                            await waitJump.UntilAsync(element,
                                Conditions.ElementIsVisible(By.CssSelector(".WB_expand_media_box")));
                        }

                        var weiboInfos = (await Task.WhenAll(weibos.Select(async t => new WeiboInfo
                        {
                            Id = t.GetAttribute("mid"),
                            RawHtml = t.GetAttribute("innerHTML"),
                            Url = t.FindElementX(By.CssSelector(".WB_func li:nth-child(2) a"))?.GetAttribute("href") ??
                                  "",
                            ImgUrls = (await Task.WhenAll(PulloutImgList(
                                    t.FindElementX(By.CssSelector(".WB_media_a[action-data]"))
                                        ?.GetAttribute("action-data") ?? "")
                                .Select(async d => new Img
                                {
                                    ImgUrl = $"http://wx1.sinaimg.cn/large/{d}",
                                    ImgPath = await DownloadImg($"http://wx1.sinaimg.cn/large/{d}")
                                }))).ToList()
                        }))).AsEnumerable();

                        IList<WeiboInfo> weiboInfoList;

                        using (var db = new Database())
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            weiboInfoList = weiboInfos.Where(t => db.WeiboInfo.All(d => t.Id != d.Id)).ToList();
                            await db.WeiboInfo.AddRangeAsync(weiboInfoList);
                            await db.SaveChangesAsync();
                        }

                        File.Delete("screenshot.png");
                        ((ITakesScreenshot) webDriver).GetScreenshot().SaveAsFile("screenshot.png");
                        Log.Logger.Information($"Find {weiboInfoList.Count} new weibos");
                        if (weiboInfoList.Count > 0)
                            Log.Logger.Information("Passing weibos to telegram bot...");
                        foreach (var weiboInfo in weiboInfoList)
                            WeiboReceived?.Invoke(this, new WeiboEventArgs {WeiboInfo = weiboInfo});
                    }
                    catch (Exception e)
                    {
                        if (e is WebDriverException)
                        {
                            Log.Fatal(e, "Browser dead");
                            return;
                        }

                        Log.Fatal(e, "Access Weibo failed");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1));
                }

                //close Chrome
                webDriver.Close();
            }
        }

        private IEnumerable<string> PulloutImgList(string html)
        {
            var listOnlyNinePic = ImgUrlRegex.Matches(html).Select(d => d.Value);
            var allPic = ImgUrlRegexNew.Match(html).Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => $"{t}.jpg");
            return listOnlyNinePic.Concat(allPic).Distinct();
        }

        private async Task Login(IWebDriver webDriver)
        {
            var wait = new AsyncWait(TimeSpan.FromSeconds(30));
            await wait.UntilAsync(webDriver, ExpectedConditions.ElementIsVisible(By.Name("username")));

            var username = webDriver.FindElement(By.Name("username"));
            var password = webDriver.FindElement(By.Name("password"));
            var submitBtn =
                webDriver.FindElement(By.CssSelector("div[node-type='normal_form'] a[node-type='submitBtn']"));

            username.Clear();
            username.SendKeys(Program.Config["Weibo:Username"]);

            password.Clear();
            password.SendKeys(Program.Config["Weibo:Password"]);
            submitBtn.Click();

            while (true)
                try
                {
                    await wait.UntilAsync(webDriver,
                        ExpectedConditions.ElementIsVisible(By.CssSelector(".WB_left_nav")));
                    break;
                }
                catch (TimeoutException)
                {
                    Code = "";
                    File.Delete("verify.png");
                    ((ITakesScreenshot) webDriver).GetScreenshot().SaveAsFile("verify.png");
                    Console.WriteLine("Please check verify.png for verify code");
                    using (var verifyImgStream =
                        new MemoryStream(((ITakesScreenshot) webDriver).GetScreenshot().AsByteArray))
                    {
                        VerifyRequested?.Invoke(this, new VerifyEventArgs {VerifyImg = verifyImgStream});
                    }

                    while (string.IsNullOrWhiteSpace(Code))
                    {
                        Log.Logger.Information("Waiting for verify code...");
                        await Task.Delay(5000);
                    }

                    webDriver.FindElement(By.CssSelector(".verify input")).SendKeys(Code);
                    submitBtn.Click();
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