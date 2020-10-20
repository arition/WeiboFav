using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace WeiboFav.Utils
{
    public static class Utils
    {
        public static async Task<string> GetAttributeAsync(this ElementHandle element, string name, Page page)
        {
            return await page.EvaluateFunctionAsync<string>($"(el) => el.getAttribute(\"{name}\")", element);
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
                            Size = new Size(1000, 1000),
                            Mode = ResizeMode.Min,
                            Sampler = new BicubicResampler()
                        }));
                    image.SaveAsJpeg(ms, new JpegEncoder {Quality = 75, Subsample = JpegSubsample.Ratio420});
                }

                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }

        public static Stream ResizeImage(Stream stream)
        {
            using (stream)
            {
                var ms = new MemoryStream();
                using (var image = Image.Load(stream))
                {
                    // ReSharper disable AccessToDisposedClosure
                    image.Mutate(x => x.Crop(new Rectangle(0, 0,
                        image.Width, image.Height / 2)));
                    image.SaveAsJpeg(ms, new JpegEncoder { Quality = 75, Subsample = JpegSubsample.Ratio420 });
                }

                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }
    }
}