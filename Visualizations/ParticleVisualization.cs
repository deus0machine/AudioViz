using OpenTK.Graphics.OpenGL;
using System;

namespace AudioViz.Visualizations
{
    public class ParticleVisualization : IVisualization
    {
        private readonly ShaderManager _shaderManager;
        private int _vao, _vbo;
        private readonly float[] _vertices = new float[512 * 3];
        private const int Particles = 256;

        public string Name => "Particle System";

        public ParticleVisualization(ShaderManager shaderManager)
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

            GL.Enable(EnableCap.ProgramPointSize);
        }

        public void Render(double time, AudioCapture audioCapture)
        {
            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);
            float rms = audioCapture.GetRMS();

            for (int i = 0; i < Particles; i++)
            {
                float angle = (float)i / Particles;
                int fftIndex = (i * fftData.Length) / Particles / 8;
                float intensity = Math.Min(fftData[fftIndex] * 4.0f + rms * 10.0f, 1.0f); // Увеличена чувствительность

                _vertices[i * 3] = angle;
                _vertices[i * 3 + 1] = intensity;
                _vertices[i * 3 + 2] = intensity;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _vertices.Length * sizeof(float), _vertices);

            int shader = _shaderManager.GetShader("particles");
            GL.UseProgram(shader);
            GL.Uniform1(GL.GetUniformLocation(shader, "uTime"), (float)time);
            GL.Uniform2(GL.GetUniformLocation(shader, "uResolution"), new OpenTK.Mathematics.Vector2(1400, 800));

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Points, 0, Particles);
        }

        public void Cleanup()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }
}

