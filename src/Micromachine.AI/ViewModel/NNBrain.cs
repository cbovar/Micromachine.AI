using System;
using System.Collections.Generic;
using System.Diagnostics;
using ConvNetSharp.Core;
using ConvNetSharp.Core.Fluent;
using ConvNetSharp.Core.Training.Single;
using ConvNetSharp.Volume;
using ConvNetSharp.Volume.Single;

namespace Micromachine.AI.ViewModel
{
    /// <summary>
    /// Neural network brain
    /// </summary>
    internal class NNBrain : BaseViewModel, IBrain
    {
        private readonly List<Tuple<float[], Direction>> _trainingSet = new List<Tuple<float[], Direction>>();
        private float _loss;

        private INet<float> _network;

        public NNBrain(int totalInputPoints)
        {
            this.TotalInputPoints = totalInputPoints;
            CreateNetwork(totalInputPoints);
        }

        public int TotalInputPoints { get; }

        public float Loss
        {
            get => this._loss;
            set
            {
                this._loss = value;
                OnPropertyChanged();
            }
        }

        public int TrainingCount => this._trainingSet.Count;

        public void AddTrainingData(float[] data, Direction direction)
        {
            this._trainingSet.Add(new Tuple<float[], Direction>(data, direction));
            OnPropertyChanged(nameof(this.TrainingCount));
        }

        public void Reset()
        {
            this._trainingSet.Clear();
            this.Loss = 0.0f;
            CreateNetwork(this.TotalInputPoints);


            OnPropertyChanged(nameof(this.TrainingCount));
            OnPropertyChanged(nameof(this.Loss));

        }

        public Direction Evaluate(float[] data)
        {
            var input = BuilderInstance.Volume.From(data, new Shape(1, 1, this.TotalInputPoints, 1));
            this._network.Forward(input); // evaluate network

            var direction = (Direction) this._network.GetPrediction()[0]; // one-hot encoded -> label
            return direction;
        }

        public void Train()
        {
            // Create input and output volumes
            var batchSize = this._trainingSet.Count;
            var input = BuilderInstance.Volume.SameAs(new Shape(1, 1, this.TotalInputPoints, batchSize));
            var output = BuilderInstance.Volume.SameAs(new Shape(1, 1, 3, batchSize));

            for (var i = 0; i < batchSize; i++)
            {
                for (var j = 0; j < this.TotalInputPoints; j++)
                {
                    input.Set(0, 0, j, i, this._trainingSet[i].Item1[j]);
                }

                output.Set(0, 0, (int) this._trainingSet[i].Item2, i, 1.0f); // one-hot encoded output
            }

            var trainer = new SgdTrainer(this._network) {LearningRate = 0.1f, BatchSize = batchSize};

            // Learn until loss converges
            float previousLoss;
            do
            {
                previousLoss = trainer.Loss;
                trainer.Train(input, output);
                Debug.WriteLine(trainer.Loss);
            } while (Math.Abs(previousLoss - trainer.Loss) > 0.01);

            this.Loss = (float) Math.Round(trainer.Loss, 2);
        }

        private void CreateNetwork(int totalInputPoints)
        {
            // A very simple network
            this._network = FluentNet<float>
                .Create(1, 1, totalInputPoints)
                .FullyConn(3)
                .Softmax(3)
                .Build();
        }
    }
}