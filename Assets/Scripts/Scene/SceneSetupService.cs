using UnityEngine;
using Unity.XR.CoreUtils;

namespace UrbanScanVR.Scene
{
    /// <summary>
    /// Автоматическая подготовка сцены после импорта OBJ.
    /// Нормализация позиции, освещение, коллайдеры, позиция игрока.
    /// </summary>
    public class SceneSetupService : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Максимальный размер одного BoxCollider для чанков модели")]
        [SerializeField] float maxColliderChunkSize = 20f;

        /// <summary>
        /// Полная подготовка сцены: нормализация → коллайдеры → свет → игрок.
        /// Вызывается после успешного импорта OBJ.
        /// </summary>
        public void SetupScene(GameObject importedModel)
        {
            if (importedModel == null) return;

            Debug.Log("[SceneSetup] Начинаю подготовку сцены...");

            // 1. Нормализуем позицию модели
            NormalizePosition(importedModel);

            // 2. Добавляем коллайдеры
            AddColliders(importedModel);

            // 3. Настраиваем освещение
            SetupLighting();

            // 4. Позиционируем игрока
            PositionPlayer(importedModel);

            // 5. Отключаем тени на импортированных мешах (производительность)
            DisableShadows(importedModel);

            Debug.Log("[SceneSetup] Сцена подготовлена!");
        }

        /// <summary>
        /// Нормализация: центр X/Z на origin, нижняя точка на Y=0
        /// </summary>
        void NormalizePosition(GameObject model)
        {
            var bounds = CalculateCombinedBounds(model);

            if (bounds.size == Vector3.zero)
            {
                Debug.LogWarning("[SceneSetup] Модель пустая, нечего нормализовать");
                return;
            }

            // Смещаем так, чтобы центр X/Z был на (0,0), а низ модели на Y=0
            var offset = new Vector3(
                -bounds.center.x,
                -bounds.min.y,
                -bounds.center.z
            );

            model.transform.position += offset;

            Debug.Log($"[SceneSetup] Нормализация: bounds={bounds.size}, offset={offset}");
        }

        /// <summary>
        /// Добавляет упрощённые коллайдеры.
        /// Стратегия: пол (BoxCollider) + BoxCollider на каждый дочерний Mesh.
        /// </summary>
        void AddColliders(GameObject model)
        {
            var bounds = CalculateCombinedBounds(model);

            // Пол — большой тонкий BoxCollider на Y=0
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(model.transform.parent);
            floor.transform.position = new Vector3(0, -0.05f, 0);
            var floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(
                bounds.size.x + 20f,  // с запасом
                0.1f,
                bounds.size.z + 20f
            );
            floor.layer = 0; // Default layer

            // BoxCollider на каждый дочерний MeshFilter
            int colliderCount = 0;
            var meshFilters = model.GetComponentsInChildren<MeshFilter>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                var meshBounds = mf.sharedMesh.bounds;

                // Пропускаем слишком мелкие чанки
                if (meshBounds.size.magnitude < 0.1f) continue;

                // Для больших чанков используем BoxCollider (быстрее MeshCollider)
                var boxCol = mf.gameObject.AddComponent<BoxCollider>();
                boxCol.center = meshBounds.center;
                boxCol.size = meshBounds.size;

                colliderCount++;
            }

            Debug.Log($"[SceneSetup] Добавлено коллайдеров: {colliderCount} + 1 пол");
        }

        /// <summary>Настройка освещения: нейтральное, без художественных эффектов</summary>
        void SetupLighting()
        {
            // Ищем существующий Directional Light
            var existingLight = FindAnyObjectByType<Light>();
            if (existingLight != null && existingLight.type == LightType.Directional)
            {
                // Уже есть — обновляем настройки
                existingLight.color = new Color(1f, 0.96f, 0.9f);
                existingLight.intensity = 1.2f;
                existingLight.shadows = LightShadows.None; // без теней для производительности
            }

            // Ambient light — мягкий заполняющий свет
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
        }

        /// <summary>Ставим VR-игрока внутри сцены</summary>
        void PositionPlayer(GameObject model)
        {
            var xrOrigin = FindAnyObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[SceneSetup] XR Origin не найден!");
                return;
            }

            var bounds = CalculateCombinedBounds(model);

            // Ставим игрока в центр модели на уровне пола
            // Высота камеры ~1.7м задаётся XR трекингом автоматически
            var spawnPoint = new Vector3(
                0f,
                0f,  // на уровне пола
                0f
            );

            xrOrigin.transform.position = spawnPoint;
            xrOrigin.transform.rotation = Quaternion.identity;

            Debug.Log($"[SceneSetup] Игрок размещён в {spawnPoint}");
        }

        /// <summary>Отключаем тени на импортированных мешах для VR-производительности</summary>
        void DisableShadows(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Вычисляет общий Bounds всех мешей в иерархии</summary>
        Bounds CalculateCombinedBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
