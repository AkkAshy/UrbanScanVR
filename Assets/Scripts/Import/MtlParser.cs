using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace UrbanScanVR.Import
{
    /// <summary>
    /// Парсер MTL файлов (материалы для OBJ).
    /// Поддерживает: Kd, Ka, Ks, Ns, d/Tr, map_Kd, map_Bump, illum.
    /// </summary>
    public static class MtlParser
    {
        /// <summary>Данные одного материала из MTL файла</summary>
        public class MtlData
        {
            public string Name;
            public Color AmbientColor = Color.black;       // Ka
            public Color DiffuseColor = Color.white;       // Kd
            public Color SpecularColor = Color.black;      // Ks
            public float Shininess = 10f;                   // Ns (0-1000)
            public float Alpha = 1f;                        // d (1 = opaque)
            public string DiffuseTexturePath;               // map_Kd
            public string NormalMapPath;                    // map_Bump / bump
            public int IlluminationModel = 2;               // illum
        }

        static readonly CultureInfo INV = CultureInfo.InvariantCulture;

        /// <summary>Асинхронный парсинг MTL файла</summary>
        public static Task<Dictionary<string, MtlData>> ParseAsync(string filePath)
        {
            return Task.Run(() => Parse(filePath));
        }

        /// <summary>Синхронный парсинг MTL</summary>
        static Dictionary<string, MtlData> Parse(string filePath)
        {
            var materials = new Dictionary<string, MtlData>();

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[MtlParser] Файл не найден: {filePath}");
                return materials;
            }

            MtlData current = null;

            foreach (var line in File.ReadLines(filePath))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;

                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("newmtl "))
                {
                    // Новый материал
                    string name = trimmed.Substring(7).Trim();
                    current = new MtlData { Name = name };
                    materials[name] = current;
                }
                else if (current == null)
                {
                    continue; // игнорируем строки до первого newmtl
                }
                else if (trimmed.StartsWith("Kd "))
                {
                    current.DiffuseColor = ParseColor(trimmed.Substring(3));
                }
                else if (trimmed.StartsWith("Ka "))
                {
                    current.AmbientColor = ParseColor(trimmed.Substring(3));
                }
                else if (trimmed.StartsWith("Ks "))
                {
                    current.SpecularColor = ParseColor(trimmed.Substring(3));
                }
                else if (trimmed.StartsWith("Ns "))
                {
                    // Shininess: OBJ = 0-1000, Unity Smoothness = 0-1
                    float ns = float.Parse(trimmed.Substring(3).Trim(), INV);
                    current.Shininess = ns;
                }
                else if (trimmed.StartsWith("d "))
                {
                    // Dissolve (прозрачность): 1 = opaque, 0 = transparent
                    current.Alpha = float.Parse(trimmed.Substring(2).Trim(), INV);
                }
                else if (trimmed.StartsWith("Tr "))
                {
                    // Transparency (обратная dissolve): 0 = opaque, 1 = transparent
                    current.Alpha = 1f - float.Parse(trimmed.Substring(3).Trim(), INV);
                }
                else if (trimmed.StartsWith("map_Kd "))
                {
                    current.DiffuseTexturePath = trimmed.Substring(7).Trim();
                }
                else if (trimmed.StartsWith("map_Bump ") || trimmed.StartsWith("bump "))
                {
                    int start = trimmed.StartsWith("map_Bump ") ? 9 : 5;
                    current.NormalMapPath = trimmed.Substring(start).Trim();
                }
                else if (trimmed.StartsWith("illum "))
                {
                    current.IlluminationModel = int.Parse(trimmed.Substring(6).Trim(), INV);
                }
            }

            return materials;
        }

        /// <summary>Парсинг цвета из "r g b" строки</summary>
        static Color ParseColor(string str)
        {
            var parts = str.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                float r = float.Parse(parts[0], INV);
                float g = float.Parse(parts[1], INV);
                float b = float.Parse(parts[2], INV);
                return new Color(r, g, b, 1f);
            }
            return Color.white;
        }

        /// <summary>
        /// Создаёт Unity Material из MtlData.
        /// Использует URP Lit шейдер. Текстуры загружаются отдельно.
        /// </summary>
        public static Material CreateMaterial(MtlData mtlData, Texture2D diffuseTex = null,
            Texture2D normalMap = null)
        {
            // Определяем шейдер
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = mtlData.Name;

            // Базовый цвет
            Color baseColor = mtlData.DiffuseColor;
            baseColor.a = mtlData.Alpha;
            mat.SetColor("_BaseColor", baseColor);

            // Smoothness (Ns 0-1000 → 0-1)
            float smoothness = Mathf.Clamp01(mtlData.Shininess / 1000f);
            mat.SetFloat("_Smoothness", smoothness);

            // Metallic (по умолчанию 0 для LiDAR сканов)
            mat.SetFloat("_Metallic", 0f);

            // Диффузная текстура
            if (diffuseTex != null)
            {
                mat.SetTexture("_BaseMap", diffuseTex);
            }

            // Normal map
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Прозрачность
            if (mtlData.Alpha < 0.99f)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000; // Transparent queue
            }

            return mat;
        }
    }
}
