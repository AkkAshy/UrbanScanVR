using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace UrbanScanVR.Import
{
    /// <summary>
    /// Загрузка текстур с диска в рантайме.
    /// Поддерживает PNG, JPG/JPEG (через ImageConversion.LoadImage).
    /// </summary>
    public static class TextureLoader
    {
        /// <summary>
        /// Загружает текстуру из файла.
        /// Чтение байтов — в фоновом потоке, создание Texture2D — в главном.
        /// </summary>
        /// <param name="texturePath">Абсолютный путь к файлу текстуры</param>
        /// <param name="basePath">Базовая директория OBJ файла (для относительных путей)</param>
        public static async Task<Texture2D> LoadAsync(string texturePath, string basePath = null)
        {
            // Определяем полный путь
            string fullPath = texturePath;

            if (!Path.IsPathRooted(texturePath) && basePath != null)
            {
                fullPath = Path.Combine(basePath, texturePath);
            }

            // Нормализуем путь (обратные слеши → прямые)
            fullPath = fullPath.Replace('\\', '/');

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[TextureLoader] Текстура не найдена: {fullPath}");
                return null;
            }

            // Читаем байты в фоновом потоке
            byte[] fileData = await Task.Run(() => File.ReadAllBytes(fullPath));

            // Создаём текстуру в главном потоке (Unity API)
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            texture.name = Path.GetFileNameWithoutExtension(fullPath);

            if (ImageConversion.LoadImage(texture, fileData))
            {
                // Настройки текстуры
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 4;

                return texture;
            }

            Debug.LogWarning($"[TextureLoader] Не удалось загрузить текстуру: {fullPath}");
            Object.Destroy(texture);
            return null;
        }
    }
}
