using System;
using OpenQA.Selenium;

namespace WeiboFav.Utils
{
    public class Conditions
    {
        private static IWebElement ElementIfVisible(IWebElement element)
        {
            if (!element.Displayed)
                return null;
            return element;
        }

        public static Func<IWebElement, IWebElement> ElementIsVisible(By locator)
        {
            return driver =>
            {
                try
                {
                    return ElementIfVisible(driver.FindElement(locator));
                }
                catch (StaleElementReferenceException ex)
                {
                    return (IWebElement) null;
                }
            };
        }
    }
}