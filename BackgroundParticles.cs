using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace AudioViz
{
    public class BackgroundParticles : IDisposable
    {
        private const int ParticleCount = 200; // Уменьшил количество для отладки
        private readonly Particle[] _particles;
        private readonly Random _random = new Random();
        private int _vao, _vbo;
        private readonly ShaderManager _shaderManager;
        private double _lastTime;
        private int _particlesAlive = 0; // Счётчик живых частиц

        public BackgroundParticles(ShaderManager shaderManager)
        {
            _shaderManager = shaderManager;
            _particles = new Particle[ParticleCount];
            
            InitializeParticles();
            SetupBuffers();
        }

        private void InitializeParticles()
        {
            for (int i = 0; i < ParticleCount; i++)
            {
                _particles[i] = CreateNewParticle();
            }
            _particlesAlive = ParticleCount;
            Console.WriteLine($"Initialized {_particlesAlive} particles");
        }

        private Particle CreateNewParticle()
        {
            return new Particle
            {
                Position = new Vector2(
                    (float)(_random.NextDouble() * 2.4f - 1.2f), // X по всей ширине экрана
                    1.0f + (float)_random.NextDouble() * 0.5f    // Y чуть выше видимой области
                ),
                Velocity = new Vector2(
                    (float)(_random.NextDouble() - 0.5) * 0.002f, // Случайное горизонтальное движение
                    -0.01f - (float)_random.NextDouble() * 0.01f  // Постоянное движение вниз
                ),
                Color = new Vector3(
                    (float)_random.NextDouble() * 0.7f + 0.3f,
                    (float)_random.NextDouble() * 0.7f + 0.3f,
                    (float)_random.NextDouble() * 0.7f + 0.3f
                ),
                Size = (float)_random.NextDouble() * 8 + 6,
                Life = 1.0f,
                MaxLife = 1.0f
            };
        }

        private void SetupBuffers()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, ParticleCount * 8 * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, 8 * sizeof(float), 5 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, 8 * sizeof(float), 7 * sizeof(float));
            GL.EnableVertexAttribArray(4);

            GL.BindVertexArray(0);
        }

        public void Update(double time, AudioCapture audioCapture, Vector2[]? barForces = null)
        {
            float deltaTime = (float)(time - _lastTime);
            if (deltaTime <= 0) return;
            
            _lastTime = time;

            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);
            float rms = audioCapture.GetRMS();

            float musicEnergy = Math.Min(rms * 8f, 1f);
            int respawnedCount = 0;

            for (int i = 0; i < ParticleCount; i++)
            {
                var particle = _particles[i];

                // Сохраняем исходную жизнь для проверки
                float originalLife = particle.Life;

                // Базовая физика - движение вниз
                Vector2 newPosition = particle.Position + particle.Velocity * deltaTime * 60;
                Vector2 newVelocity = particle.Velocity;

                // Слабая гравитация вниз
                newVelocity.Y -= 0.0002f;

                // Взаимодействие с барами - ОТТАЛКИВАНИЕ
                if (barForces != null && barForces.Length > 0)
                {
                    foreach (var barPos in barForces)
                    {
                        Vector2 toParticle = newPosition - barPos;
                        float distance = toParticle.Length;
                        
                        // Если частица близко к бару - отталкиваем
                        if (distance < 0.3f && distance > 0.01f)
                        {
                            float forceStrength = 0.0008f / (distance * distance);
                            newVelocity += toParticle * forceStrength;
                            
                            // Добавляем вертикальный импульс вверх при отталкивании
                            newVelocity.Y += 0.001f / distance;
                        }
                    }
                }

                // Реакция на музыку - более мягкая
                float bass = GetFrequencyBand(audioCapture, 0, 50);
                float mid = GetFrequencyBand(audioCapture, 50, 500);
                float high = GetFrequencyBand(audioCapture, 500, 2000);

                // Случайные колебания от музыки
                newVelocity += new Vector2(
                    (float)(_random.NextDouble() - 0.5) * bass * 0.001f,
                    (float)(_random.NextDouble() - 0.5) * bass * 0.0005f
                );

                // Цвет реагирует на музыку
                Vector3 newColor = new Vector3(
                    0.4f + mid * 0.6f,
                    0.4f + high * 0.6f,
                    0.4f + bass * 0.6f
                );

                float newSize = 6 + high * 6 + musicEnergy * 4;

                // Сопротивление воздуха - очень слабое
                newVelocity *= 0.999f;

                // Границы экрана по X - отскакивание от стен
                if (newPosition.X < -1.3f)
                {
                    newVelocity.X = Math.Abs(newVelocity.X) * 0.7f;
                    newPosition.X = -1.3f;
                }
                else if (newPosition.X > 1.3f)
                {
                    newVelocity.X = -Math.Abs(newVelocity.X) * 0.7f;
                    newPosition.X = 1.3f;
                }

                // Обновление жизни - ОЧЕНЬ МЕДЛЕННОЕ
                float newLife = particle.Life - (0.0001f * deltaTime * 60);

                // Если частица упала слишком низко или умерла от старости - перерождаем её сверху
                if (newPosition.Y < -1.5f || newLife <= 0)
                {
                    _particles[i] = CreateNewParticle();
                    respawnedCount++;
                    continue;
                }

                _particles[i] = new Particle
                {
                    Position = newPosition,
                    Velocity = newVelocity,
                    Color = newColor,
                    Size = newSize,
                    Life = newLife,
                    MaxLife = particle.MaxLife
                };
            }

        }

        private float GetFrequencyBand(AudioCapture audioCapture, int startFreq, int endFreq)
        {
            var fftData = new float[2048];
            audioCapture.GetFFTData(fftData);
            
            float sum = 0;
            int count = 0;
            int startIndex = Math.Max(1, startFreq / 10);
            int endIndex = Math.Min(fftData.Length - 1, endFreq / 10);
            
            for (int i = startIndex; i < endIndex; i++)
            {
                sum += fftData[i];
                count++;
            }
            
            return count > 0 ? Math.Min(sum / count * 1.5f, 1f) : 0;
        }

        public void Render(double time)
        {
            float[] particleData = new float[ParticleCount * 8];
            int index = 0;
            int visibleParticles = 0;
            
            foreach (var particle in _particles)
            {
                particleData[index++] = particle.Position.X;
                particleData[index++] = particle.Position.Y;
                particleData[index++] = particle.Color.X;
                particleData[index++] = particle.Color.Y;
                particleData[index++] = particle.Color.Z;
                particleData[index++] = particle.Size;
                particleData[index++] = particle.Life;
                particleData[index++] = particle.MaxLife;
                
                if (particle.Life > 0) visibleParticles++;
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);
            GL.Enable(EnableCap.ProgramPointSize);

            int shader = _shaderManager.GetShader("backgroundParticles");
            if (shader == 0) 
            {
                Console.WriteLine("Background particles shader not found!");
                return;
            }
            
            GL.UseProgram(shader);
            GL.Uniform1(GL.GetUniformLocation(shader, "uTime"), (float)time);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, particleData.Length * sizeof(float), particleData);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Points, 0, ParticleCount);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }

    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector3 Color;
        public float Size;
        public float Life;
        public float MaxLife;
    }
}