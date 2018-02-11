using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConvNetSharp.Core;
using ConvNetSharp.Core.Layers.Single;
using ConvNetSharp.Core.Training.Single;
using ConvNetSharp.Volume;
using ConvNetSharp.Volume.Single;
using Micromachine.AI.Service;

namespace Micromachine.AI.ViewModel
{
    internal class ViewModel : BaseViewModel
    {
        private readonly ImageService _imageService;

        private readonly List<Tuple<float[], Direction>> _trainingSet = new List<Tuple<float[], Direction>>();

        private readonly float maxSpeed = 3.0f;

        private bool _autoMode;

        private ICommand _autoModeCommand;

        private ICommand _resetNetworkCommand;

        private ICommand _someCommand;

        public object Locker = new object(); // used to synchronise camera data between threads

        public ViewModel()
        {
            WriteableBitmap writeableBitmap;
            this._imageService = new ImageService(this);
            this.ImageSource = writeableBitmap = this._imageService.CreateImage(900, 600);
            CompositionTarget.Rendering += (o, e) =>
            {
                UpdateCarCoordinates();
                this._imageService.UpdateImage(writeableBitmap);
            };

            CreateNetwork();
        }

        public float Angle { get; set; }

        public float X { get; set; } = 120.0f;

        public float Y { get; set; } = 200.0f;

        public float Speed { get; set; }

        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        public ICommand TeachCommand
        {
            get { return this._someCommand ?? (this._someCommand = new RelayCommand(o => true, o => Teach((Direction) int.Parse((string) o)))); }
        }

        public ICommand AutoModeCommand
        {
            get
            {
                return this._autoModeCommand ?? (this._autoModeCommand = new RelayCommand(o => true, o =>
                {
                    this._autoMode = !this._autoMode;
                    this.Logs.Add($"AutoMode = {this._autoMode}");
                }));
            }
        }

        public ICommand ResetNetworkCommand
        {
            get { return this._resetNetworkCommand ?? (this._resetNetworkCommand = new RelayCommand(o => true, o => CreateNetwork())); }
        }

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

        private void AddNewTraining(Direction direction)
        {
            if (this._imageService.CameraInput != null)
            {
                this._trainingSet.Add(new Tuple<float[], Direction>((float[]) this._imageService.CameraInput.Clone(), direction));
                OnPropertyChanged("TrainingCount");
            }
        }

        private void CreateNetwork()
        {
            this._network = new Net<float>();
            this._network.AddLayer(new InputLayer(1, 1, 200));
            this._network.AddLayer(new FullyConnLayer(100));
            this._network.AddLayer(new FullyConnLayer(3));
            this._network.AddLayer(new SoftmaxLayer(3));
        }

        private void Rotate(Direction direction)
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

        public void Teach(Direction direction)
        {
            this.Logs.Add($"Teach {direction}");
            if (this.Logs.Count > 5)
            {
                this.Logs.RemoveAt(0);
            }

            lock (this.Locker)
            {
                AddNewTraining(direction);
            }

            Train();
        }

        private void Train()
        {
            // Create input and output volumes
            var batchSize = this._trainingSet.Count;
            var rawInput = new float[batchSize * 200];
            var input = BuilderInstance.Volume.From(rawInput, new Shape(1, 1, 200, batchSize));
            var oneHotEncoded = new float[3 * batchSize];
            var output = BuilderInstance.Volume.From(oneHotEncoded, new Shape(1, 1, 3, batchSize));

            for (var i = 0; i < batchSize; i++)
            {
                for (var j = 0; j < 200; j++)
                {
                    input.Set(0, 0, j, i, this._trainingSet[i].Item1[j]);
                }

                output.Set(0, 0, (int) this._trainingSet[i].Item2, i, 1.0f);
            }

            var trainer = new SgdTrainer(this._network) {LearningRate = 0.01f, BatchSize = batchSize, L2Decay = 0.1f, L1Decay = 0.1f};

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

        public void UpdateCarCoordinates()
        {
            if (!this._autoMode)
            {
                if (Keyboard.IsKeyDown(Key.Right) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    Rotate(Direction.Right);
                }

                if (Keyboard.IsKeyDown(Key.Left) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    Rotate(Direction.Left);
                }

                if (Keyboard.IsKeyDown(Key.Up) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Speed = this.maxSpeed;
                }
                else if (Keyboard.IsKeyDown(Key.Down) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Speed = -this.maxSpeed;
                }
                else
                {
                    this.Speed = 0.0f;
                }
            }
            else
            {
                var input = BuilderInstance.Volume.From(this._imageService.CameraInput, new Shape(1, 1, 200, 1));
                this._network.Forward(input);

                var direction = (Direction) this._network.GetPrediction()[0];
                Rotate(direction);

                this.Speed = this.maxSpeed;
            }

            this.X += this.Speed * (float) Math.Sin(this.Angle / 360.0 * 2 * Math.PI);
            this.Y -= this.Speed * (float) Math.Cos(this.Angle / 360.0 * 2 * Math.PI);
        }

        #region ImageSource

        private ImageSource _imageSource;
        private Net<float> _network;
        private float _loss;

        public ImageSource ImageSource
        {
            get => this._imageSource;
            set
            {
                if (this._imageSource != value)
                {
                    this._imageSource = value;
                    OnPropertyChanged(nameof(this.ImageSource));
                }
            }
        }

        #endregion
    }
}