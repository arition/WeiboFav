using System.IO;
using OpenQA.Selenium;
using SixLabors.ImageSharp;

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

        /// <summary>
        /// Identify a picture and reset the stream head
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static IImageInfo IdentifyX(Stream stream)
        {
            var result = Image.Identify(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }
}