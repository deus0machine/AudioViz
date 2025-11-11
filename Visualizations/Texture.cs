using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL;

namespace AudioViz
{
    public class Texture : IDisposable
    {
        private readonly int _handle;
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public Texture(string path)
        {
            _handle = GL.GenTexture();
            string finalPath = path;
            // Попробуем путь относительно исполняемого файла
            if (!File.Exists(finalPath))
            {
                finalPath = Path.Combine(AppContext.BaseDirectory, path);
            }
            if (!File.Exists(finalPath))
            {
                throw new FileNotFoundException($"Texture file not found: {path} (tried {finalPath})");
            }
            LoadFromFile(finalPath);
        }

        private void LoadFromFile(string path)
        {
            GL.BindTexture(TextureTarget.Texture2D, _handle);

            // Важная настройка выравнивания, чтобы не было артефактов
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            using (var bitmap = new Bitmap(path))
            {
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var data = bitmap.LockBits(
                    rect,
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    bitmap.Width,
                    bitmap.Height,
                    0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    data.Scan0);

                bitmap.UnlockBits(data);
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public void Use(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, _handle);
        }

        public void Dispose()
        {
            GL.DeleteTexture(_handle);
        }
    }
}
