namespace Micromachine.AI.ViewModel
{
    internal interface IBrain
    {
        void AddTrainingData(float[] data, Direction direction);

        Direction Evaluate(float[] data);

        void Reset();

        void Train();
    }
}