using OpenTK.Graphics.OpenGL;
using System;
using OpenTK.Windowing.Desktop;

namespace AudioViz
{
    public class BackgroundRenderer : IDisposable
    {
        private readonly int _vao, _vbo;
        private readonly Texture _texture;
        private readonly ShaderManager _shaderManager;

        public BackgroundRenderer(ShaderManager shaderManager, string texturePath)
        {
            _shaderManager = shaderManager;
            _texture = new Texture(texturePath);
  
            float[] vertices = {
                -1.0f, -1.0f, 0.0f, 0.0f,  // left-bottom
                 1.0f, -1.0f, 1.0f, 0.0f,  // right-bottom
                 1.0f,  1.0f, 1.0f, 1.0f,  // right-top
                -1.0f,  1.0f, 0.0f, 1.0f   // left-top
            };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            
            // Текстурные координаты
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        public void Render(double time)
        {
            // Включаем blending и отключаем depth test
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.DepthTest);

            int shader = _shaderManager.GetShader("background");
            if (shader == 0)
            {
                Console.WriteLine("Background shader not found!");
                return;
            }

            GL.UseProgram(shader);
            
            // Активируем текстуру
            _texture.Use(TextureUnit.Texture0);
            GL.Uniform1(GL.GetUniformLocation(shader, "uBackground"), 0);
            GL.Uniform1(GL.GetUniformLocation(shader, "uTime"), (float)time);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            _texture?.Dispose();
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }
}