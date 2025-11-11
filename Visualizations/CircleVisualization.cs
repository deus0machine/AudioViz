using OpenTK.Graphics.OpenGL;
using System;

namespace AudioViz.Visualizations
{
    public class CircleVisualization : IVisualization
    {
        private readonly ShaderManager _shaderManager;
        private int _vao, _vbo, _ebo;

        private const int Bars = 64;

        // 4 vertices per bar, 3 floats per vertex: angleNorm, radius, intensity
        private readonly float[] _vertices = new float[Bars * 4 * 3];
        private readonly uint[] _indices = new uint[Bars * 6];

        private readonly float[] _smoothed = new float[Bars];

        private const float Attack = 0.6f;
        private const float Decay = 0.06f;

        private float _beatValue = 0f;
        private const float BeatAttack = 0.25f;
        private const float BeatDecay = 0.015f;
        private const float BeatSensitivity = 1.35f;
        private float _prevBass = 0f;

        private const float InnerRadius = 0.28f;
        private const float MaxBarLength = 0.55f;

        // Угол ширины бара (доля от полного круга). Меньше = тоньше бары.
        // Полный круг = 1.0 (по нормализованной системе angleNorm 0..1).
        private const float BarAngularWidth = 0.9f / Bars; // tweak: 0.9 сохраняет небольшой gap между барами

        public string Name => "Circle Bars";

        public CircleVisualization(ShaderManager shaderManager)
        {
            _shaderManager = shaderManager;
        }

        public void Setup()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            // VBO
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);

            // EBO (ВАЖНО: он должен быть привязан ПОСЛЕ BindVertexArray)
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), IntPtr.Zero, BufferUsageHint.StreamDraw);

            // layout
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 3 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0); // <-- теперь можно отключить
}

        public void Render(double time, AudioCapture audioCapture)
        {
            // Отключаем depth/cull на всякий случай
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);

            // BEAT detection (нижние бины)
            int bassBins = Math.Min(20, fftData.Length);
            float bassSum = 0f;
            for (int i = 0; i < bassBins; i++) bassSum += Math.Abs(fftData[i]);
            float bassAvg = bassSum / Math.Max(1, bassBins);

            if (bassAvg > _prevBass * BeatSensitivity)
                _beatValue = Math.Min(1f, _beatValue + BeatAttack);

            _beatValue = Math.Max(0f, _beatValue - BeatDecay);
            _prevBass = bassAvg;

            int v = 0;
            int ind = 0;
            uint bi = 0;

            for (int i = 0; i < Bars; i++)
            {
                int fftIndex = (int)((long)i * fftData.Length / Bars);
                if (fftIndex < 0) fftIndex = 0;
                if (fftIndex >= fftData.Length) fftIndex = fftData.Length - 1;

                float raw = Math.Abs(fftData[fftIndex]);
                float magnitude = Math.Min((float)Math.Sqrt(raw) * 5f, 1f);

                float prev = _smoothed[i];
                _smoothed[i] = magnitude > prev ? prev + (magnitude - prev) * Attack
                                                : prev + (magnitude - prev) * Decay;
                float sm = _smoothed[i];

                // нормализованный угол центра бара (0..1)
                float centerAngle = (float)i / Bars;

                // половина угла для левого/правого края бара
                float half = BarAngularWidth * 0.5f;

                float angleLeft = centerAngle - half;
                float angleRight = centerAngle + half;

                // чтобы не получить выход за 0..1 для последнего/первого — нормализуем
                if (angleLeft < 0f) angleLeft += 1f;
                if (angleRight >= 1f) angleRight -= 1f;

                float rInner = InnerRadius;
                float rOuter = InnerRadius + sm * MaxBarLength;

                // Заполняем 4 вершины: left-inner, right-inner, right-outer, left-outer
                // Для передачи в шейдер: aPos.x = angleNorm (0..1), aPos.y = radius
                // intensity = sm

                // left-inner
                _vertices[v++] = angleLeft; _vertices[v++] = rInner; _vertices[v++] = sm;
                // right-inner
                _vertices[v++] = angleRight; _vertices[v++] = rInner; _vertices[v++] = sm;
                // right-outer
                _vertices[v++] = angleRight; _vertices[v++] = rOuter; _vertices[v++] = sm;
                // left-outer
                _vertices[v++] = angleLeft; _vertices[v++] = rOuter; _vertices[v++] = sm;

                // Индексы (два треугольника): (0,1,2) и (0,2,3) в локальной системе
                _indices[ind++] = bi;
                _indices[ind++] = bi + 1;
                _indices[ind++] = bi + 2;
                _indices[ind++] = bi;
                _indices[ind++] = bi + 2;
                _indices[ind++] = bi + 3;

                bi += 4;
            }

            // Загружаем данные в буферы
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, v * sizeof(float), _vertices);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, ind * sizeof(uint), _indices);

            int shader = _shaderManager.GetShader("circlebars");
            if (shader == 0)
                return;

            GL.UseProgram(shader);

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
    }
}
