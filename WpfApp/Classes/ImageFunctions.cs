﻿using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace WpfApp
{
    static class ImageFunctions
    {
        /// <summary>
        /// Converts base64 string to image.
        /// </summary>
        /// <param name="imageBytes">Image bytes.</param>
        /// <returns>Image.</returns>
        public static Image BytesToImage(byte[] imageBytes)
        {
            // converts byte[] to Image
            using var stream = new MemoryStream(imageBytes);
            Image image = Image.FromStream(stream, true);
            return image;
        }

        public static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        /// <summary>
        /// Resize image to specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using var wrapMode = new ImageAttributes();
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }

            return destImage;
        }

        /// <summary>
        /// Generates HTML code for displaying logos in SVG format.
        /// </summary>
        /// <param name="logo">SVG logo.</param>
        /// <param name="maxWidth">Width of the logo container.</param>
        /// <param name="maxHeight">Height of the logo container.</param>
        /// <returns>Html code.</returns>
        public static string GenerateSvgLogoHtml(byte[] logo, int maxWidth, int maxHeight)
        {
            string base64 = System.Convert.ToBase64String(logo);
            return
                "<!DOCTYPE html>" +
                    "<html>" +
                        "<head>" +
                            "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                            "<style>" +
                                "html,body {" +
                                    "margin: 2.5px 0 0 0;" +
                                    "padding: 0;" +
                                    "overflow: hidden;" +
                                    "display: flex;" +
                                    "justify-content: center;" +
                                "}" +
                                "img {" +
                                    "max-width:" + maxWidth + "px;" +
                                    "max-height:" + maxHeight + "px;" +
                                    "width: auto;" +
                                    "height: auto;" +
                                "}" +
                            "</style>" +
                       "</head>" +
                       "<body>" +
                            "<img src=\'data:image/svg+xml;base64," + base64 + "\'>" +
                       "</body>" +
                   "</html>";
        }
    }
}
