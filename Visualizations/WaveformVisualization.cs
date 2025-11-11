using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace AudioViz.Visualizations
{
    public class WaveformVisualization : IVisualization
    {
        private readonly ShaderManager _shaderManager;
        private int _vao, _vbo;
        private readonly float[] _vertices = new float[4096 * 3];

        public string Name => "Waveform";

        public WaveformVisualization(ShaderManager shaderManager)
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

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 3 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        public void Render(double time, AudioCapture audioCapture)
        {
            audioCapture.GetAudioData(new float[4096]);
            var audioData = new float[4096];
            audioCapture.GetAudioData(audioData);

            for (int i = 0; i < audioData.Length; i++)
            {
                float x = (float)i / (audioData.Length - 1);
                float y = Math.Abs(audioData[i]) * 5.0f; // Увеличена амплитуда
                float intensity = Math.Min(Math.Abs(audioData[i]) * 5.0f, 1.0f); // Увеличена интенсивность
                _vertices[i * 3] = x;
                _vertices[i * 3 + 1] = y;
                _vertices[i * 3 + 2] = intensity;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _vertices.Length * sizeof(float), _vertices);

            int shader = _shaderManager.GetShader("waveform");
            GL.UseProgram(shader);
            GL.Uniform1(GL.GetUniformLocation(shader, "uTime"), (float)time);
            GL.Uniform2(GL.GetUniformLocation(shader, "uResolution"), new Vector2(1400, 800));

            GL.BindVertexArray(_vao);
            GL.LineWidth(5.0f); // Увеличена толщина линии
            GL.DrawArrays(PrimitiveType.LineStrip, 0, audioData.Length);
        }

        public void Cleanup()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }
}

