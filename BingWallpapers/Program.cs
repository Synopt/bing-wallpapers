﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BingWallpapers
{
    internal static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        static uint SPI_SETDESKWALLPAPER = 20;
        private static uint SPIF_UPDATEINIFILE = 0x1;

        static void Main()
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                var input = "0";
                while (IsValidInput(input))
                {
                    var offset = int.Parse(input);
                    Console.WriteLine(PrettyPrintOffset(offset));

                    MainAsync(offset).Wait(cts.Token);

                    Console.WriteLine("Enter a number of days (0-14) to offset the selection by, or anything else to quit.\n");
                    input = Console.ReadLine();
                    Console.Write("\n\n");
                }
            }
            catch (AggregateException e)
            {
                Console.WriteLine(e.InnerException);
                Console.ReadKey();
            }
        }

        private static bool IsValidInput(string input)
        {
            int result;
            if (int.TryParse(input, out result))
            {
                return result >= 0 && result <= 14;
            }
            return false;
        }

        private static string PrettyPrintOffset(int offset)
        {
            var start = "Wallpapers";

            if (offset == 0)
            {
                return $"{start} for today:";
            }
            if (offset == 1)
            {
                return $"{start} for yesterday:";
            }
            else
            {
                return $"{start} from {offset} days ago:";
            }
        }

        static async Task MainAsync(int offset)
        {
            var metadata = await GetWallpaperMetadata(2, offset).ConfigureAwait(false);

            var downloadTasks = metadata.Select(DownloadWallpaper);

            var wallpapers = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

            var images = new Dictionary<string, Image>
            {
                {@"\\.\DISPLAY1", Image.FromFile(wallpapers[0])},
                {@"\\.\DISPLAY2", Image.FromFile(wallpapers[1])}
            };

            CreateBackgroundImage(images);
        }

        static async Task<IEnumerable<ImageMetadata>> GetWallpaperMetadata(int number, int offset)
        {
            var metadata = new List<ImageMetadata>();

            using (var client = new WebClient())
            {
                var url = new Uri($"http://www.bing.com/HPImageArchive.aspx?format=js&idx={offset}&n={number}&cc=gb&video=0");
                var json = await client.DownloadStringTaskAsync(url).ConfigureAwait(false);
                var content = JsonConvert.DeserializeObject<dynamic>(json);

                foreach (var image in content.images)
                {
                    var name = FormatName((string)image.url);
                    var description = FormatDescription((string)image.copyright);
                    metadata.Add(new ImageMetadata((string)image.url, name, description));
                }
            }

            return metadata;
        }

        private static string FormatDescription(string copyright)
        {
            return copyright.Replace("Â", "").Replace("(", "\n(");
        }

        private static string FormatName(string url)
        {
            var startOfName = url.LastIndexOf('/') + 1;
            return url.Substring(startOfName, url.IndexOf('_', startOfName) - startOfName).SplitPascalCase();
        }

        static async Task<string> DownloadWallpaper(ImageMetadata metadata)
        {
            var directory = $@"{ Environment.GetEnvironmentVariable("USERPROFILE")}\Pictures\Bing Wallpapers";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var imgLocation = Path.Combine(directory, $"{metadata.Name}.jpg");

            if (!File.Exists(imgLocation))
            {
                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync("http://www.bing.com" + metadata.Url, imgLocation).ConfigureAwait(false);
                }
            }

            Console.WriteLine($"{metadata.Name}: {metadata.Description}");
            Console.WriteLine();

            return imgLocation;
        }

        static void CreateBackgroundImage(Dictionary<string, Image> imageFiles)
        {
            string defaultBackgroundFile = $@"{Environment.GetEnvironmentVariable("USERPROFILE")}\Pictures\Bing Wallpapers\Current.bmp";

            using (var virtualScreenBitmap = new Bitmap((int)System.Windows.SystemParameters.VirtualScreenWidth, (int)System.Windows.SystemParameters.VirtualScreenHeight))
            {
                using (var virtualScreenGraphic = Graphics.FromImage(virtualScreenBitmap))
                {
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        var image = (imageFiles.ContainsKey(screen.DeviceName)) ? imageFiles[screen.DeviceName] : null;

                        var monitorDimensions = screen.Bounds;
                        var width = monitorDimensions.Width;
                        var monitorBitmap = new Bitmap(width, monitorDimensions.Height);
                        var fromImage = Graphics.FromImage(monitorBitmap);
                        fromImage.FillRectangle(SystemBrushes.Desktop, 0, 0, monitorBitmap.Width, monitorBitmap.Height);

                        if (image != null)
                            DrawImageCentered(fromImage, image, new Rectangle(0, 0, monitorBitmap.Width, monitorBitmap.Height));

                        Rectangle rectangle;
                        if (monitorDimensions.Top == 0 && monitorDimensions.Left == 0)
                        {
                            rectangle = monitorDimensions;
                        }
                        else
                        {
                            if ((monitorDimensions.Left < 0 && monitorDimensions.Width > -monitorDimensions.Left) ||
                                (monitorDimensions.Top < 0 && monitorDimensions.Height > -monitorDimensions.Top))
                            {
                                var isMain = (monitorDimensions.Top < 0 && monitorDimensions.Bottom > 0);

                                var left = (monitorDimensions.Left < 0)
                                    ? (int)System.Windows.SystemParameters.VirtualScreenWidth + monitorDimensions.Left
                                    : monitorDimensions.Left;

                                var top = (monitorDimensions.Top < 0)
                                    ? (int)System.Windows.SystemParameters.VirtualScreenHeight + monitorDimensions.Top
                                    : monitorDimensions.Top;

                                Rectangle srcRect;
                                if (isMain)
                                {
                                    rectangle = new Rectangle(left, 0, monitorDimensions.Width, monitorDimensions.Bottom);
                                    srcRect = new Rectangle(0, -monitorDimensions.Top, monitorDimensions.Width, monitorDimensions.Height + monitorDimensions.Top);
                                }
                                else
                                {
                                    rectangle = new Rectangle(0, top, monitorDimensions.Right, monitorDimensions.Height);
                                    srcRect = new Rectangle(-monitorDimensions.Left, 0, monitorDimensions.Width + monitorDimensions.Left,
                                        monitorDimensions.Height);
                                }

                                virtualScreenGraphic.DrawImage(monitorBitmap, rectangle, srcRect, GraphicsUnit.Pixel);
                                rectangle = new Rectangle(left, top, monitorDimensions.Width, monitorDimensions.Height);
                            }
                            else
                            {
                                var left = (monitorDimensions.Left < 0)
                                    ? (int)System.Windows.SystemParameters.VirtualScreenWidth + monitorDimensions.Left
                                    : monitorDimensions.Left;
                                var top = (monitorDimensions.Top < 0)
                                    ? (int)System.Windows.SystemParameters.VirtualScreenHeight + monitorDimensions.Top
                                    : monitorDimensions.Top;
                                rectangle = new Rectangle(left, top, monitorDimensions.Width, monitorDimensions.Height);
                            }
                        }
                        virtualScreenGraphic.DrawImage(monitorBitmap, rectangle);
                    }

                    virtualScreenBitmap.Save(defaultBackgroundFile, ImageFormat.Bmp);
                }
            }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0u, defaultBackgroundFile, SPIF_UPDATEINIFILE);
        }


        static void DrawImageCentered(Graphics g, Image img, Rectangle monitorRect)
        {
            var heightRatio = monitorRect.Height / (float)img.Height;
            var widthRatio = monitorRect.Width / (float)img.Width;
            int height;
            int width;
            var x = 0;
            var y = 0;

            if (heightRatio > 1f && widthRatio > 1f)
            {
                height = img.Height;
                width = img.Width;
                x = (int)((monitorRect.Width - width) / 2f);
                y = (int)((monitorRect.Height - height) / 2f);
            }
            else
            {
                if (heightRatio < widthRatio)
                {
                    width = (int)(img.Width * heightRatio);
                    height = (int)(img.Height * heightRatio);
                    x = (int)((monitorRect.Width - width) / 2f);
                }
                else
                {
                    width = (int)(img.Width * widthRatio);
                    height = (int)(img.Height * widthRatio);
                    y = (int)((monitorRect.Height - height) / 2f);
                }
            }

            var rect = new Rectangle(x, y, width, height);
            g.DrawImage(img, rect);
        }
    }
}
