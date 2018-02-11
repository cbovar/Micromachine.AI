using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Micromachine.AI.Service
{
    internal class ImageService
    {
        private readonly SKBitmap _carBmp;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly SKBitmap _trackBmp;
        private readonly ViewModel.ViewModel _vm;

        private int _call;

        public ImageService(ViewModel.ViewModel vm)
        {
            this._vm = vm;

            this._trackBmp = SKBitmap.Decode(@"./Images/track.png");
            this._carBmp = SKBitmap.Decode(@"./Images/car.png");
        }

        public float[] CameraInput { get; private set; }

        public WriteableBitmap CreateImage(int width, int height)
        {
            return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
        }

        /// <summary>
        /// Draw track + car + camera grid + camera output
        /// </summary>
        /// <param name="writeableBitmap"></param>
        public void UpdateImage(WriteableBitmap writeableBitmap)
        {
            int width = (int) writeableBitmap.Width,
                height = (int) writeableBitmap.Height;

            writeableBitmap.Lock();

            using (var surface = SKSurface.Create(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                writeableBitmap.BackBuffer,
                width * 4))
            {
                var canvas = surface.Canvas;

                var paint = new SKPaint {Color = new SKColor(0, 0, 0), TextSize = 16};

                // Draw track
                canvas.DrawBitmap(this._trackBmp, new SKRect(0, 0, width, height));

                // Draw FPS
                if (this._call == 0)
                {
                    this._stopwatch.Start();
                }
                var fps = this._call / (this._stopwatch.Elapsed.TotalSeconds != 0 ? this._stopwatch.Elapsed.TotalSeconds : 1);
                canvas.DrawText($"FPS: {fps:0}", 5, 16, paint);
                canvas.DrawText($"Frames: {this._call++}", 5, 32, paint);

                // Draw car
                var carCenterX = this._vm.X + this._carBmp.Width / 2.0f;
                var carCenterY = this._vm.Y + this._carBmp.Height / 2.0f;

                canvas.RotateDegrees(this._vm.Angle, carCenterX, carCenterY);
                canvas.DrawBitmap(this._carBmp, new SKRect(this._vm.X, this._vm.Y, this._vm.X + this._carBmp.Width, this._vm.Y + this._carBmp.Height));

                // Camera 
                var frontDistance = 5.0f;
                var xDensity = 0.4f;
                var yDensity = 0.2f;
                var xGrid = 10;
                var yGrid = 10;

                lock (this._vm.Locker)
                {
                    if (this.CameraInput == null)
                    {
                        this.CameraInput = new float[xGrid * 2 * yGrid];
                    }

                    // Get camera input
                    var dstinf = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var bitmap = new SKBitmap(dstinf);
                    var dstpixels = bitmap.GetPixels();

                    for (var x = -xGrid; x < xGrid; x++)
                    {
                        for (var y = 0; y < yGrid; y++)
                        {
                            var cameraX = carCenterX + x * 1.0f / xDensity;
                            var cameraY = this._vm.Y - y * 1.0f / yDensity - frontDistance;

                            var point = canvas.TotalMatrix.MapPoint(cameraX, cameraY);

                            surface.ReadPixels(dstinf, dstpixels, dstinf.RowBytes, (int) point.X, (int) point.Y);

                            var color = bitmap.GetPixel(0, 0);
                            this.CameraInput[x + xGrid + (yGrid - y - 1) * xGrid * 2] = Math.Min((color.Red + color.Blue + color.Green) / (3 * 255.0f), 255.0f); // convert to grayscale
                        }
                    }
                }

                // Draw camera grid
                for (var x = -xGrid; x < xGrid; x++)
                {
                    for (var y = 0; y < yGrid; y++)
                    {
                        var cameraX = carCenterX + x * 1.0f / xDensity;
                        var cameraY = this._vm.Y - y * 1.0f / yDensity - frontDistance;
                        canvas.DrawPoint(cameraX, cameraY, SKColors.Black);
                    }
                }

                canvas.RotateDegrees(-this._vm.Angle, carCenterX, carCenterY);

                // Draw camera output
                var zoom = 5.0f;

                for (var x = -xGrid; x < xGrid; x++)
                {
                    for (var y = 0; y < yGrid; y++)
                    {
                        var c = (byte) (this.CameraInput[x + xGrid + y * xGrid * 2] * 255.0);
                        var color = new SKColor(c, c, c);
                        var paintRect = new SKPaint {Color = color};

                        canvas.DrawRect(new SKRect(width - xGrid * zoom + x * zoom, y * zoom, width - xGrid * zoom + (x + 1) * zoom, (y + 1) * zoom), paintRect);
                    }
                }

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                writeableBitmap.Unlock();
            }
        }
    }
}