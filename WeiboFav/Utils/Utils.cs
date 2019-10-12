﻿using System.IO;
using OpenQA.Selenium;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

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

        public static Stream ResaveImage(Stream stream)
        {
            using (stream)
            {
                var ms = new MemoryStream();
                using (var image = Image.Load(stream))
                {
                    image.Mutate(x => x.Resize(
                        new ResizeOptions
                        {
                            Size = new Size(3000, 3000),
                            Mode = ResizeMode.Min,
                            Sampler = new CatmullRomResampler()
                        }));
                    image.SaveAsJpeg(ms, new JpegEncoder {Quality = 75, Subsample = JpegSubsample.Ratio420});
                }

                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
    }
}