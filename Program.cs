using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using AudioViz.Visualizations;

namespace AudioViz
{
    public class AudioVisualizer : GameWindow
    {
        private AudioCapture? _audioCapture;
        private ShaderManager? _shaderManager;
        private BackgroundRenderer? _background;
        private BackgroundParticles? _backgroundParticles;
        private readonly List<IVisualization> _visualizations = new List<IVisualization>();
        private int _currentVisualizationIndex = 0;
        private double _time;
        private float _transitionTime = 0.0f;
        private const float TransitionDuration = 1.0f;

        public AudioVisualizer() : base(GameWindowSettings.Default,
            new NativeWindowSettings()
            {
                ClientSize = new Vector2i(1400, 800),
                Title = "Epic Audio Visualizer - SPACE to change mode, ESC to exit",
                WindowBorder = WindowBorder.Resizable,
                StartVisible = false,
                StartFocused = true,
                Vsync = VSyncMode.On
            })
        {
            CenterWindow();
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            // Настройка OpenGL
            GL.ClearColor(0f, 0f, 0f, 1.0f); // Чуть светлее фон для контраста
            //GL.ClearColor(1f, 1f, 1f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);

            // Инициализация компонентов
            _shaderManager = new ShaderManager();
            _shaderManager.LoadAllShaders();
            try
            {
                _background = new BackgroundRenderer(_shaderManager, "ваапв.png");
                _backgroundParticles = new BackgroundParticles(_shaderManager);
                Console.WriteLine("Background created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load background: {ex.Message}");
            }
            _audioCapture = new AudioCapture();
            _audioCapture.Start();

            // Создание визуализаций
            _visualizations.Add(new WaveformVisualization(_shaderManager));
            _visualizations.Add(new SpectrumVisualization(_shaderManager));
            _visualizations.Add(new CircleVisualization(_shaderManager));
            _visualizations.Add(new BarsVisualization(_shaderManager));
            _visualizations.Add(new ParticleVisualization(_shaderManager));

            // Настройка визуализаций
            foreach (var viz in _visualizations)
            {
                viz.Setup();
            }

            IsVisible = true;
            UpdateTitle();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            if (_visualizations.Count > 0 && _audioCapture != null)
            {
                if (_background != null)
                {
                    _background.Render(_time);
                    _backgroundParticles?.Render(_time);
                }
                else
                {
                    Console.WriteLine("Background is null!");
                }
                // Плавный переход между визуализациями
                float alpha = Math.Min(_transitionTime / TransitionDuration, 1.0f);

                // Рендерим текущую визуализацию
                if (_visualizations.Count > 0 && _audioCapture != null)
                {
                    _visualizations[_currentVisualizationIndex].Render(_time, _audioCapture);
                }

                //Если идет переход, рендерим следующую с прозрачностью
                if (_transitionTime > 0 && _transitionTime < TransitionDuration)
                {
                    int nextIndex = (_currentVisualizationIndex + 1) % _visualizations.Count;
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    // Можно добавить более сложную логику перехода здесь
                }

                _transitionTime += (float)e.Time;
                if (_transitionTime > TransitionDuration)
                {
                    _transitionTime = 0.0f;
                }
            }

            SwapBuffers();
            _time += e.Time;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            Vector2[]? barForces = null; // Используем nullable тип
            if (_currentVisualizationIndex == 3) // Если активен Bars режим
            {
                var barsViz = _visualizations[3] as BarsVisualization;
                barForces = barsViz?.GetBarPositions();
            }
            
            _backgroundParticles?.Update(_time, _audioCapture, barForces);

            if (KeyboardState.IsKeyDown(Keys.Escape))
                Close();

            if (KeyboardState.IsKeyPressed(Keys.Space))
            {
                _currentVisualizationIndex = (_currentVisualizationIndex + 1) % _visualizations.Count;
                _transitionTime = 0.0f;
                UpdateTitle();
            }

            // Изменение размера окна
            if (KeyboardState.IsKeyPressed(Keys.F11))
            {
                WindowState = WindowState == OpenTK.Windowing.Common.WindowState.Fullscreen
                    ? OpenTK.Windowing.Common.WindowState.Normal
                    : OpenTK.Windowing.Common.WindowState.Fullscreen;
            }
        }

        private void UpdateTitle()
        {
            if (_visualizations.Count > 0)
            {
                Title = $"Epic Audio Visualizer - {_visualizations[_currentVisualizationIndex].Name} - " +
                       $"SPACE: change mode | F11: fullscreen | ESC: exit";
            }
        }

        protected override void OnUnload()
        {
            _audioCapture?.Dispose();
            _background?.Dispose();
            _backgroundParticles?.Dispose();

            foreach (var viz in _visualizations)
            {
                viz.Cleanup();
            }
            
            _shaderManager?.Dispose();
            
            base.OnUnload();
        }

        [STAThread]
        static void Main()
        {
            using var game = new AudioVisualizer();
            game.Run();
        }
    }
}
