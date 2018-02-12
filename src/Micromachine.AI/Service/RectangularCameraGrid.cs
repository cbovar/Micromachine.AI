using System;

namespace Micromachine.AI.Service
{
    internal class RectangularCameraGrid : ICameraGrid
    {
        public RectangularCameraGrid(int w, int h)
        {
            this.Width = w;
            this.Height = h;
        }

        public float XDensity { get; set; } = 0.4f;

        public float YDensity { get; set; } = 0.2f;

        public float FrontDistance { get; set; } = 5.0f;

        public int Height { get; }

        public void Apply(float carX, float carY, float carWidth, Action<int, int, float, float> action)
        {
            var carCenterX = carX + carWidth / 2.0f;

            for (var i = -this.Width; i < this.Width; i++)
            {
                for (var j = 0; j < this.Height; j++)
                {
                    var cameraX = carCenterX + i * 1.0f / this.XDensity;
                    var cameraY = carY - j * 1.0f / this.YDensity - this.FrontDistance;
                    action(i, j, cameraX, cameraY);
                }
            }
        }

        public int TotalPoints => this.Height * this.Width * 2;

        public int Width { get; }
    }
}