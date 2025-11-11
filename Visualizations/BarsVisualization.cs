using OpenTK.Graphics.OpenGL;
using System;
using OpenTK.Mathematics;

namespace AudioViz.Visualizations
{
    public class BarsVisualization : IVisualization
    {
        private readonly ShaderManager _shaderManager;
        private int _vao, _vbo, _ebo;
        private const int Bars = 64;

        private readonly float[] _vertices; // layout: normX, y, intensity
        private readonly uint[] _indices;
        private readonly float[] _smoothed;

        // smoothing
        private const float Attack = 0.6f;
        private const float Decay = 0.06f;

        // visuals
        // Максимальная высота делаем близкой к полному диапазону NDC (-1..+1).
        // Чтобы верх мог доходить почти до +1, используем ~1.95 (параметр умножается на sm и добавляется к -1).
        private const float MaxBarHeight = 1.95f;
        private const float BarWidthFactor = 0.96f; // оставляем тонкий gap между барами

        // Beat detection
        private float _beatValue = 0.0f;
        private const float BeatAttack = 0.25f;
        private const float BeatDecay = 0.015f;
        private const float BeatSensitivity = 1.35f;
        private float _prevBass = 0f;

        public string Name => "Bar Spectrum";

        public BarsVisualization(ShaderManager shaderManager)
        {
            _shaderManager = shaderManager;
            _vertices = new float[Bars * 4 * 3]; // 4 verts per bar, (normX, y, intensity)
            _indices = new uint[Bars * 6];
            _smoothed = new float[Bars];
        }

        public void Setup()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), IntPtr.Zero, BufferUsageHint.StreamDraw);

            // layout: location 0 = vec2 (normX, y), location 1 = float intensity
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 3 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        public void Render(double time, AudioCapture audioCapture)
        {
            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);

            // ---- BEAT DETECTOR ----
            int bassRange = Math.Min(20, fftData.Length); // первые бины — низкие частоты
            float bassSum = 0f;
            for (int i = 0; i < bassRange; i++) bassSum += Math.Abs(fftData[i]);
            float bassAvg = bassSum / bassRange;

            if (bassAvg > _prevBass * BeatSensitivity)
            {
                _beatValue += BeatAttack;
                if (_beatValue > 1f) _beatValue = 1f;
            }

            _beatValue -= BeatDecay;
            if (_beatValue < 0f) _beatValue = 0f;
            _prevBass = bassAvg;

            // ---- BARS (compute vertices) ----
            float cellWidth = 1f / Bars;
            float actualBarWidth = cellWidth * BarWidthFactor;
            int v = 0;
            int ind = 0;
            uint bi = 0;

            for (int i = 0; i < Bars; i++)
            {
                // распределение fft индекса (простое равномерное)
                int fftIndex = (int)((long)i * fftData.Length / Bars);
                if (fftIndex < 0) fftIndex = 0;
                if (fftIndex >= fftData.Length) fftIndex = fftData.Length - 1;

                float raw = Math.Abs(fftData[fftIndex]);
                // perceptual scaling (sqrt) для более приятного отклика
                float magnitude = Math.Min((float)Math.Sqrt(raw) * 6f, 1f);

                float prev = _smoothed[i];
                _smoothed[i] = magnitude > prev ? prev + (magnitude - prev) * Attack : prev + (magnitude - prev) * Decay;
                float sm = _smoothed[i];

                // позиции по X в нормализованной системе 0..1
                float normXLeft = i * cellWidth;
                float normXRight = normXLeft + actualBarWidth;
                // Для последнего бара, чтобы точно доходил до 1 — clamp
                if (i == Bars - 1) normXRight = 1.0f;

                // нижняя точка всегда -1
                float yBottom = -1f;
                // верх — ровно так, без волны (волну добавим в вертексе)
                float yTop = -1f + sm * MaxBarHeight;

                // vertices: left-bottom, right-bottom, right-top, left-top
                _vertices[v++] = normXLeft; _vertices[v++] = yBottom; _vertices[v++] = sm;
                _vertices[v++] = normXRight; _vertices[v++] = yBottom; _vertices[v++] = sm;
                _vertices[v++] = normXRight; _vertices[v++] = yTop; _vertices[v++] = sm;
                _vertices[v++] = normXLeft; _vertices[v++] = yTop; _vertices[v++] = sm;

                // indices
                _indices[ind++] = bi;
                _indices[ind++] = bi + 1;
                _indices[ind++] = bi + 2;
                _indices[ind++] = bi;
                _indices[ind++] = bi + 2;
                _indices[ind++] = bi + 3;

                bi += 4;
            }

            // upload buffers
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, v * sizeof(float), _vertices);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, ind * sizeof(uint), _indices);

            int shader = _shaderManager.GetShader("bars");
            if (shader == 0) return;

            GL.UseProgram(shader);

            // uniforms: время и бит
            int locTime = GL.GetUniformLocation(shader, "uTime");
            if (locTime >= 0) GL.Uniform1(locTime, (float)time);
            int locBeat = GL.GetUniformLocation(shader, "uBeat");
            if (locBeat >= 0) GL.Uniform1(locBeat, _beatValue);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, ind, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        public void Cleanup()
        {
            GL.DeleteBuffer(_ebo);
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
        public Vector2[] GetBarPositions()
        {
            Vector2[] positions = new Vector2[Bars];
            float barWidth = 2.0f / Bars;
            
            for (int i = 0; i < Bars; i++)
            {
                float x = -1.0f + i * barWidth + barWidth * 0.5f;
                float y = -1.0f + _smoothed[i] * 0.5f;
                positions[i] = new Vector2(x, y);
            }
            
            return positions;
        }
    }
    
}
