using System;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace WeiboFav.Utils
{
    public class AsyncWait
    {
        /// <summary>
        ///     Construct a new AsyncWait class
        /// </summary>
        /// <param name="timeout">Throw an exception after timeout. TimeSpan.Zero = no timeout.</param>
        /// <param name="pollingInterval">Interval in milliseconds</param>
        public AsyncWait(TimeSpan timeout, int pollingInterval = 250)
        {
            PollingInterval = pollingInterval;
            Timeout = timeout;
        }

        private int PollingInterval { get; }
        private TimeSpan Timeout { get; }

        public async Task UntilAsync(Func<bool> condition)
        {
            var startTime = DateTime.UtcNow;
            while (true)
            {
                if (condition()) break;
                await Task.Delay(PollingInterval);
                if (DateTime.UtcNow - startTime > Timeout) throw new TimeoutException();
            }
        }

        public async Task UntilAsync<TResult>(IWebDriver driver, Func<IWebDriver, TResult> condition)
        {
            await UntilAsync(() =>
            {
                try
                {
                    var result = condition(driver);
                    if (result is bool) return (bool) (object) result;
                    return result != null;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task UntilAsync<TResult>(IWebElement element, Func<IWebElement, TResult> condition)
        {
            await UntilAsync(() =>
            {
                try
                {
                    var result = condition(element);
                    if (result is bool) return (bool)(object)result;
                    return result != null;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}