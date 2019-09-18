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
        private Regex FileNameRegex { get; } = new Regex(@"[^\/]+(?=\/$|$)", RegexOptions.Compiled);

        public event EventHandler<WeiboEventArgs> WeiboReceived;

        public async void StartScrape()
        {
            var browserDriverPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var userDirPath = new DirectoryInfo(Program.Config["Chrome:UserDirPath"]);
            if (!userDirPath.Exists) userDirPath.Create();

            var options = new ChromeOptions();
            options.AddArguments("--headless", "--disable-gpu", $"--user-data-dir={userDirPath.FullName}");

            using (var webDriver = new ChromeDriver(browserDriverPath, options))
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
                    url = "http://weibo.com/fav";
                    webDriver.Navigate().GoToUrl(url);
                    Log.Logger.Information("Checking fav Weibo...");

                    await waitJump.UntilAsync(webDriver,
                        ExpectedConditions.ElementIsVisible(By.CssSelector(".WB_feed_like")));
                    var weibos = webDriver.FindElements(By.CssSelector(".WB_feed_like"));
                    var weiboInfos = (await Task.WhenAll(weibos.Select(async t => new WeiboInfo
                    {
                        Id = t.GetAttribute("mid"),
                        RawHtml = t.GetAttribute("innerHTML"),
                        Url = t.FindElementX(By.CssSelector(".WB_func li:nth-child(2) a"))?.GetAttribute("href") ?? "",
                        ImgUrls = (await Task.WhenAll(
                            ImgUrlRegex.Matches(
                                    t.FindElementX(By.CssSelector(".WB_media_a[action-data]"))
                                        ?.GetAttribute("action-data") ?? "").Select(d => d.Value).Distinct()
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

                    await Task.Delay(TimeSpan.FromMinutes(1));
                }

                //close Chrome
                webDriver.Close();
            }
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
                    File.Delete("verify.png");
                    ((ITakesScreenshot) webDriver).GetScreenshot().SaveAsFile("verify.png");
                    Console.WriteLine("Please check verify.png for verify code");
                    Console.Write("Verify Code: ");
                    var code = Console.ReadLine();
                    webDriver.FindElement(By.CssSelector(".verify input")).SendKeys(code);
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
    }
}