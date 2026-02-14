using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UrbanScanVR.Import
{
    /// <summary>
    /// Оркестратор импорта OBJ файлов.
    /// Связывает: диалог выбора → парсинг OBJ → парсинг MTL → загрузка текстур → создание Mesh.
    /// Отчитывается о прогрессе через события.
    /// </summary>
    public class FileImportService : MonoBehaviour
    {
        // === События ===

        /// <summary>Прогресс импорта (0-1) + текстовый статус</summary>
        public event Action<float, string> OnProgressChanged;

        /// <summary>Импорт успешно завершён — передаём корневой GameObject</summary>
        public event Action<GameObject> OnImportCompleted;

        /// <summary>Импорт провалился — передаём сообщение об ошибке</summary>
        public event Action<string> OnImportFailed;

        // === Состояние ===

        CancellationTokenSource _cts;
        bool _isImporting;

        /// <summary>Идёт ли сейчас импорт</summary>
        public bool IsImporting => _isImporting;

        /// <summary>Текущая загруженная модель</summary>
        public GameObject CurrentModel { get; private set; }

        /// <summary>
        /// Запускает полный пайплайн импорта.
        /// Открывает файловый диалог → парсит → создаёт объекты.
        /// </summary>
        public async void ImportObjFile()
        {
            if (_isImporting)
            {
                Debug.LogWarning("[FileImportService] Импорт уже идёт!");
                return;
            }

            // Открываем файловый диалог (на десктопе)
            string filePath = NativeFileDialog.OpenFile();
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.Log("[FileImportService] Выбор файла отменён");
                return;
            }

            await ImportFromPath(filePath);
        }

        /// <summary>Импорт по указанному пути (без диалога)</summary>
        public async Task ImportFromPath(string filePath)
        {
            if (_isImporting) return;

            _isImporting = true;
            _cts = new CancellationTokenSource();

            try
            {
                Debug.Log($"[FileImportService] Начинаю импорт: {filePath}");
                ReportProgress(0f, "Начинаю импорт...");

                // Удаляем предыдущую модель
                if (CurrentModel != null)
                {
                    Destroy(CurrentModel);
                    CurrentModel = null;
                }

                // === Шаг 1: Парсинг OBJ (30% прогресса) ===
                ReportProgress(0.05f, "Парсинг OBJ файла...");

                var objProgress = new Progress<float>(p =>
                    ReportProgress(0.05f + p * 0.25f, "Парсинг OBJ файла..."));

                var objData = await ObjParser.ParseAsync(filePath, objProgress, _cts.Token);

                Debug.Log($"[FileImportService] OBJ: {objData.Vertices.Count} вершин, " +
                          $"{objData.Normals.Count} нормалей, {objData.Groups.Count} групп");

                // === Шаг 2: Парсинг MTL (10% прогресса) ===
                Dictionary<string, MtlParser.MtlData> mtlDataMap = null;
                string baseDir = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(objData.MtlLibPath))
                {
                    ReportProgress(0.35f, "Загрузка материалов...");
                    string mtlPath = Path.Combine(baseDir, objData.MtlLibPath);
                    mtlDataMap = await MtlParser.ParseAsync(mtlPath);
                    Debug.Log($"[FileImportService] MTL: {mtlDataMap.Count} материалов");
                }

                // === Шаг 3: Загрузка текстур (20% прогресса) ===
                var materials = new Dictionary<string, Material>();

                if (mtlDataMap != null)
                {
                    int matIndex = 0;
                    int matCount = mtlDataMap.Count;

                    foreach (var kvp in mtlDataMap)
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        float matProgress = 0.45f + (float)matIndex / matCount * 0.2f;
                        ReportProgress(matProgress, $"Загрузка текстур ({matIndex + 1}/{matCount})...");

                        Texture2D diffuseTex = null;
                        Texture2D normalMap = null;

                        // Загружаем диффузную текстуру
                        if (!string.IsNullOrEmpty(kvp.Value.DiffuseTexturePath))
                        {
                            diffuseTex = await TextureLoader.LoadAsync(
                                kvp.Value.DiffuseTexturePath, baseDir);
                        }

                        // Загружаем normal map
                        if (!string.IsNullOrEmpty(kvp.Value.NormalMapPath))
                        {
                            normalMap = await TextureLoader.LoadAsync(
                                kvp.Value.NormalMapPath, baseDir);
                        }

                        // Создаём Unity Material
                        materials[kvp.Key] = MtlParser.CreateMaterial(kvp.Value, diffuseTex, normalMap);
                        matIndex++;
                    }
                }

                // === Шаг 4: Создание Mesh и GameObject (40% прогресса) ===
                ReportProgress(0.65f, "Создание 3D модели...");

                // BuildGameObject работает с Unity API — ТОЛЬКО в главном потоке
                var rootGO = ObjParser.BuildGameObject(objData, materials);
                rootGO.name = Path.GetFileNameWithoutExtension(filePath);

                CurrentModel = rootGO;

                ReportProgress(1f, "Импорт завершён!");
                Debug.Log($"[FileImportService] Импорт завершён: {rootGO.name}");

                OnImportCompleted?.Invoke(rootGO);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[FileImportService] Импорт отменён");
                OnImportFailed?.Invoke("Импорт отменён");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileImportService] Ошибка импорта: {ex.Message}\n{ex.StackTrace}");
                OnImportFailed?.Invoke($"Ошибка: {ex.Message}");
            }
            finally
            {
                _isImporting = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>Отменить текущий импорт</summary>
        public void CancelImport()
        {
            _cts?.Cancel();
        }

        void ReportProgress(float progress, string status)
        {
            OnProgressChanged?.Invoke(progress, status);
        }

        void OnDestroy()
        {
            CancelImport();
        }
    }
}
