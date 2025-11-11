using OpenTK.Graphics.OpenGL;
using System;

namespace AudioViz.Visualizations
{
    public class SpectrumVisualization : IVisualization
    {
        private readonly ShaderManager _shaderManager;
        private int _vao, _vbo;
        private readonly float[] _vertices = new float[2048 * 2];

        public string Name => "Frequency Spectrum";

        public SpectrumVisualization(ShaderManager shaderManager)
        {
            _shaderManager = shaderManager;
        }

        public void Setup()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
        }

        public void Render(double time, AudioCapture audioCapture)
        {
            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);

            int visibleBands = fftData.Length / 2;
            int startBand = 1; // Начинаем с первой значимой полосы

            for (int i = startBand; i < visibleBands; i++)
            {
                float x = ((float)(i - startBand) / (visibleBands - startBand)) * 1.6f - 0.8f;
                
                // УВЕЛИЧЕННАЯ ЧУВСТВИТЕЛЬНОСТЬ
                float y = Math.Min(fftData[i] * 6.0f, 1.2f); 

                _vertices[i * 2] = x;
                _vertices[i * 2 + 1] = y;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _vertices.Length * sizeof(float), _vertices);

            int shader = _shaderManager.GetShader("spectrum");
            GL.UseProgram(shader);
            GL.Uniform1(GL.GetUniformLocation(shader, "uTime"), (float)time);

            GL.BindVertexArray(_vao);
            GL.LineWidth(16.0f); // Увеличена толщина линии
            GL.DrawArrays(PrimitiveType.LineStrip, 2, visibleBands - 2);
        }

        public void Cleanup()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }
}

