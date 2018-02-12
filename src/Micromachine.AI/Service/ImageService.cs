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
        private readonly ICameraGrid _cameraGrid;

        private int _call;

        public ImageService(ViewModel.ViewModel vm, ICameraGrid cameraGrid)
        {
            this._vm = vm;
            this._cameraGrid = cameraGrid;

            this._trackBmp = SKBitmap.Decode(@"./Images/track.png");
            this._carBmp = SKBitmap.Decode(@"./Images/car.png");
        }

        public float[] CameraInput { get; private set; }

        public WriteableBitmap CreateImage(int width, int height)
        {
            return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
        }

        /// <summary>
        ///     Draw track + car + camera grid + camera output
        /// </summary>
        /// <param name="writeableBitmap"></param>
        public void UpdateImage(WriteableBitmap writeableBitmap)
        {
            int width = (int)writeableBitmap.Width,
                height = (int)writeableBitmap.Height;

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

                var paint = new SKPaint { Color = new SKColor(0, 0, 0), TextSize = 16 };

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
                lock (this._vm.Locker)
                {
                    if (this.CameraInput == null)
                    {
                        this.CameraInput = new float[this._cameraGrid.TotalPoints];
                    }

                    // Get camera input
                    var dstinf = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var bitmap = new SKBitmap(dstinf);
                    var dstpixels = bitmap.GetPixels();

                    this._cameraGrid.Apply(this._vm.X, this._vm.Y, this._carBmp.Width, (i, j, x, y) =>
                    {
                        var point = canvas.TotalMatrix.MapPoint(x, y);
                        surface.ReadPixels(dstinf, dstpixels, dstinf.RowBytes, (int)point.X, (int)point.Y);
                        var color = bitmap.GetPixel(0, 0);
                        this.CameraInput[i + this._cameraGrid.Width + (this._cameraGrid.Height - j - 1) * this._cameraGrid.Width * 2] = Math.Min((color.Red + color.Blue + color.Green) / (3 * 255.0f), 255.0f); // convert to grayscale
                    });
                }

                // Draw camera grid
                this._cameraGrid.Apply(this._vm.X, this._vm.Y, this._carBmp.Width, (i, j, x, y) => canvas.DrawPoint(x, y, SKColors.Black));

                canvas.RotateDegrees(-this._vm.Angle, carCenterX, carCenterY);

                // Draw camera output
                var zoom = 5.0f;

                this._cameraGrid.Apply(this._vm.X, this._vm.Y, this._carBmp.Width, (i, j, x, y) =>
                {
                    var c = (byte)(this.CameraInput[i + this._cameraGrid.Width + j * this._cameraGrid.Width * 2] * 255.0);
                    var color = new SKColor(c, c, c);
                    var paintRect = new SKPaint { Color = color };

                    canvas.DrawRect(new SKRect(width - this._cameraGrid.Width * zoom + i * zoom, j * zoom, width - this._cameraGrid.Width * zoom + (i + 1) * zoom, (j + 1) * zoom), paintRect);
                });

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                writeableBitmap.Unlock();
            }
        }
    }
}