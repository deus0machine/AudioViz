namespace AudioViz.Visualizations
{
    public interface IVisualization
    {
        void Render(double time, AudioCapture audioCapture);
        void Setup();
        void Cleanup();
        string Name { get; }
    }
}

