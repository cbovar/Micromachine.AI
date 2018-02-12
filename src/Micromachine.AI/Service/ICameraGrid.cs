using System;

namespace Micromachine.AI.Service
{
    interface ICameraGrid
    {
        int TotalPoints { get; }

        int Width { get; }

        int Height { get; }

        void Apply(float carX, float carY, float carWidth, Action<int, int, float, float> action);
    }
}