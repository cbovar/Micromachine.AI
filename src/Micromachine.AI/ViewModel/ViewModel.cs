using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Micromachine.AI.Service;

namespace Micromachine.AI.ViewModel
{
    internal class ViewModel : BaseViewModel
    {
        private readonly ImageService _imageService;

        public readonly object Locker = new object(); // used to synchronise camera data between threads

        private ICommand _autoModeCommand;

        private ICommand _resetNetworkCommand;

        private ICommand _someCommand;

        public ViewModel()
        {
            WriteableBitmap writableBitmap;
            var cameraGrid = new RectangularCameraGrid(10, 10);
            this._imageService = new ImageService(this, cameraGrid);
            this.ImageSource = writableBitmap = this._imageService.CreateImage(900, 600);

            this.Car = new Car(new NNBrain(cameraGrid.TotalPoints)); // Create a car and neural network 'brain'

            CompositionTarget.Rendering += (o, e) =>
            {
                this.UpdateCarCoordinates();
                this._imageService.UpdateImage(writableBitmap);
            };
        }

        public Car Car { get; }

        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        public ICommand TeachCommand
        {
            get { return this._someCommand ??= new RelayCommand(o => true, o => this.Teach((Direction) int.Parse((string) o))); }
        }

        public ICommand AutoModeCommand
        {
            get
            {
                return this._autoModeCommand ??= new RelayCommand(o => true, o =>
                {
                    this.Car.AutoPilot = !this.Car.AutoPilot;
                    this.Log($"AutoMode = {this.Car.AutoPilot}");
                });
            }
        }

        public ICommand ResetNetworkCommand
        {
            get
            {
                return this._resetNetworkCommand ??= new RelayCommand(o => true, o =>
                {
                    this.Car.Brain.Reset();
                    this.Log("Reset");
                });
            }
        }

        private void Log(string s)
        {
            this.Logs.Add(s);
            if (this.Logs.Count > 5)
            {
                this.Logs.RemoveAt(0);
            }
        }

        public void Teach(Direction direction)
        {
            this.Log($"Teach {direction}");

            lock (this.Locker)
            {
                this.Car.AddTrainingData((float[]) this._imageService.CameraInput.Clone(), direction);
            }

            this.Car.Train();
        }

        public void UpdateCarCoordinates()
        {
            if (this.Car.AutoPilot)
            {
                lock (this.Locker)
                {
                    this.Car.Evaluate(this._imageService.CameraInput);
                }
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.Right) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Rotate(Direction.Right);
                }

                if (Keyboard.IsKeyDown(Key.Left) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Rotate(Direction.Left);
                }

                if (Keyboard.IsKeyDown(Key.Up) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Accelerate();
                }
                else if (Keyboard.IsKeyDown(Key.Down) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Reverse();
                }
                else
                {
                    this.Car.Decelerate();
                }
            }

            this.Car.UpdateCarCoordinates();
        }

        #region ImageSource

        private ImageSource _imageSource;

        public ImageSource ImageSource
        {
            get => this._imageSource;
            set
            {
                if (Equals(this._imageSource, value))
                {
                    return;
                }

                this._imageSource = value;
                this.OnPropertyChanged(nameof(this.ImageSource));
            }
        }

        #endregion
    }
}