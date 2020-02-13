using System;

namespace Micromachine.AI.ViewModel
{
    internal class Car
    {
        private const float MaxSpeed = 3.0f;

        public Car(IBrain brain)
        {
            this.Brain = brain;
        }

        public IBrain Brain { get; }

        public float Angle { get; private set; }

        public float X { get; private set; } = 120.0f;

        public float Y { get; private set; } = 200.0f;

        public float Speed { get; private set; }

        public bool AutoPilot { get; set; }

        public void Accelerate()
        {
            this.Speed = MaxSpeed;
        }

        public void AddTrainingData(float[] data, Direction direction)
        {
            this.Brain.AddTrainingData(data, direction);
        }

        public void Decelerate()
        {
            this.Speed = 0;
        }

        public void Evaluate(float[] data)
        {
            var direction = this.Brain.Evaluate(data);
            this.Rotate(direction);

            this.Speed = MaxSpeed;
        }

        public void Reverse()
        {
            this.Speed = -MaxSpeed;
        }

        public void Rotate(Direction direction)
        {
            switch (direction)
            {
                case Direction.Straight:
                    break;
                case Direction.Left:
                    this.Angle -= 3;
                    break;
                case Direction.Right:
                    this.Angle += 3;
                    break;
            }
        }

        public void Train()
        {
            this.Brain.Train();
        }

        public void UpdateCarCoordinates()
        {
            this.X += this.Speed * (float)Math.Sin(this.Angle / 360.0 * 2 * Math.PI);
            this.Y -= this.Speed * (float)Math.Cos(this.Angle / 360.0 * 2 * Math.PI);
        }
    }
}