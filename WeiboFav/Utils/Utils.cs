using OpenQA.Selenium;

namespace WeiboFav.Utils
{
    public static class Utils
    {
        /// <summary>
        /// Return null when an element cannot be found
        /// </summary>
        /// <param name="webElement"></param>
        /// <param name="by"></param>
        /// <returns></returns>
        public static IWebElement FindElementX(this IWebElement webElement, By by)
        {
            try
            {
                return webElement.FindElement(by);
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        }
    }
}