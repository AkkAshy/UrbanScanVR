using UnityEngine;
using System.Collections.Generic;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Генерация процедурных текстур для UI:
    /// скруглённые прямоугольники, градиенты, круги.
    /// Текстуры кэшируются для переиспользования.
    /// </summary>
    public static class UIHelper
    {
        static readonly Dictionary<string, Texture2D> _cache = new();

        // === Цветовая палитра ===
        public static readonly Color CardBg       = new(0.10f, 0.10f, 0.18f, 0.95f);
        public static readonly Color PanelBg      = new(0.06f, 0.06f, 0.12f, 0.92f);
        public static readonly Color Accent       = new(0.25f, 0.55f, 1.00f, 1.00f);
        public static readonly Color AccentDark   = new(0.15f, 0.35f, 0.85f, 1.00f);
        public static readonly Color AccentHover  = new(0.35f, 0.65f, 1.00f, 1.00f);
        public static readonly Color TextPrimary  = new(0.95f, 0.95f, 0.97f, 1.00f);
        public static readonly Color TextSecondary= new(0.55f, 0.55f, 0.65f, 1.00f);
        public static readonly Color Border       = new(1.00f, 1.00f, 1.00f, 0.08f);
        public static readonly Color BorderLight  = new(1.00f, 1.00f, 1.00f, 0.15f);
        public static readonly Color Success      = new(0.20f, 0.85f, 0.40f, 1.00f);
        public static readonly Color Error        = new(1.00f, 0.30f, 0.30f, 1.00f);
        public static readonly Color Danger       = new(0.80f, 0.20f, 0.20f, 1.00f);
        public static readonly Color DangerDark   = new(0.55f, 0.12f, 0.12f, 1.00f);
        public static readonly Color Secondary    = new(0.18f, 0.18f, 0.28f, 0.90f);
        public static readonly Color SecondaryBorder = new(1f, 1f, 1f, 0.12f);

        /// <summary>Скруглённый прямоугольник с рамкой</summary>
        public static Sprite CreateRoundedSprite(int w, int h, int radius,
            Color fill, Color border, int borderWidth = 2)
        {
            string key = $"rounded_{w}_{h}_{radius}_{ColorKey(fill)}_{ColorKey(border)}_{borderWidth}";
            if (!_cache.TryGetValue(key, out var tex))
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dist = SdfRoundedRect(x, y, w, h, radius);

                        if (dist < -borderWidth)
                        {
                            // Внутри — заливка
                            pixels[y * w + x] = fill;
                        }
                        else if (dist < 0)
                        {
                            // Рамка
                            float t = Mathf.Clamp01(-dist / borderWidth);
                            pixels[y * w + x] = Color.Lerp(border, fill, t * t);
                        }
                        else if (dist < 1.5f)
                        {
                            // Антиалиасинг на краю
                            float alpha = 1f - Mathf.Clamp01(dist / 1.5f);
                            var c = border;
                            c.a *= alpha;
                            pixels[y * w + x] = c;
                        }
                        else
                        {
                            pixels[y * w + x] = Color.clear;
                        }
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                _cache[key] = tex;
            }

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Градиентный скруглённый прямоугольник</summary>
        public static Sprite CreateGradientSprite(int w, int h, int radius,
            Color topColor, Color bottomColor, Color border, int borderWidth = 2)
        {
            string key = $"grad_{w}_{h}_{radius}_{ColorKey(topColor)}_{ColorKey(bottomColor)}_{ColorKey(border)}";
            if (!_cache.TryGetValue(key, out var tex))
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = (float)y / h;
                    Color fill = Color.Lerp(bottomColor, topColor, t);

                    for (int x = 0; x < w; x++)
                    {
                        float dist = SdfRoundedRect(x, y, w, h, radius);

                        if (dist < -borderWidth)
                        {
                            pixels[y * w + x] = fill;
                        }
                        else if (dist < 0)
                        {
                            float bt = Mathf.Clamp01(-dist / borderWidth);
                            pixels[y * w + x] = Color.Lerp(border, fill, bt * bt);
                        }
                        else if (dist < 1.5f)
                        {
                            float alpha = 1f - Mathf.Clamp01(dist / 1.5f);
                            var c = border;
                            c.a *= alpha;
                            pixels[y * w + x] = c;
                        }
                        else
                        {
                            pixels[y * w + x] = Color.clear;
                        }
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                _cache[key] = tex;
            }

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Круг (для индикатора VR)</summary>
        public static Sprite CreateCircleSprite(int size, Color color)
        {
            string key = $"circle_{size}_{ColorKey(color)}";
            if (!_cache.TryGetValue(key, out var tex))
            {
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;

                var pixels = new Color[size * size];
                float center = size / 2f;
                float radius = center - 1f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                        if (dist < 0)
                        {
                            pixels[y * size + x] = color;
                        }
                        else if (dist < 1.5f)
                        {
                            float alpha = 1f - Mathf.Clamp01(dist / 1.5f);
                            var c = color;
                            c.a *= alpha;
                            pixels[y * size + x] = c;
                        }
                        else
                        {
                            pixels[y * size + x] = Color.clear;
                        }
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();
                _cache[key] = tex;
            }

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>SDF для скруглённого прямоугольника</summary>
        static float SdfRoundedRect(int px, int py, int w, int h, int radius)
        {
            // Расстояние от точки до скруглённого прямоугольника
            float cx = Mathf.Max(Mathf.Abs(px - w / 2f) - (w / 2f - radius), 0);
            float cy = Mathf.Max(Mathf.Abs(py - h / 2f) - (h / 2f - radius), 0);
            return Mathf.Sqrt(cx * cx + cy * cy) - radius;
        }

        /// <summary>Ключ для кэширования по цвету</summary>
        static string ColorKey(Color c) =>
            $"{(int)(c.r*255)}{(int)(c.g*255)}{(int)(c.b*255)}{(int)(c.a*255)}";
    }
}
