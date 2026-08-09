using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using MonoGameColor = Microsoft.Xna.Framework.Color;

namespace JumpGameMonoGame
{
    /// <summary>
    /// Renders text to mono-game textures at runtime using System.Drawing.
    /// This avoids the need for a Content Pipeline font file.
    /// </summary>
    public static class TextRenderer
    {
        private static readonly Dictionary<(string, int, MonoGameColor), Texture2D> Cache = new();

        /// <summary>
        /// Renders a string to a MonoGame texture with the given font size and color.
        /// Results are cached so repeated calls are cheap.
        /// </summary>
        public static Texture2D RenderText(GraphicsDevice device, string text, int fontSize, MonoGameColor color)
        {
            var key = (text, fontSize, color);
            if (Cache.TryGetValue(key, out var existing) && !existing.IsDisposed)
            {
                return existing;
            }

            using var bmp = new Bitmap(1, 1);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using var measureFont = CreateFont(fontSize);
                var size = g.MeasureString(text, measureFont);
                var width = Math.Max(1, (int)Math.Ceiling(size.Width));
                var height = Math.Max(1, (int)Math.Ceiling(size.Height));

                using var drawBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var drawG = Graphics.FromImage(drawBmp))
                {
                    drawG.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    drawG.Clear(System.Drawing.Color.Transparent);
                    using var font = CreateFont(fontSize);
                    using var brush = new SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
                    drawG.DrawString(text, font, brush, 0, 0);
                }

                var data = new byte[width * height * 4];
                var bounds = new Rectangle(0, 0, width, height);
                var bitmapData = drawBmp.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                drawBmp.UnlockBits(bitmapData);

                var texture = new Texture2D(device, width, height);
                texture.SetData(data);

                // Remove old cached entry for this key if it was disposed
                if (existing != null && existing.IsDisposed)
                {
                    Cache.Remove(key);
                }

                Cache[key] = texture;
                return texture;
            }
        }

        private static Font CreateFont(int size)
        {
            return new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static void ClearCache()
        {
            foreach (var tex in Cache.Values)
            {
                tex?.Dispose();
            }
            Cache.Clear();
        }
    }
}