#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;

namespace Editor
{
    /// <summary>
    /// Программная настройка проекта для batch mode (без Unity Editor GUI).
    /// Создаёт URP ассеты, настраивает XR/OpenXR, строит Main.unity сцену.
    /// Запуск: unity-editor -executeMethod Editor.ProjectConfigurator.ConfigureProject
    /// </summary>
    public static class ProjectConfigurator
    {
        [MenuItem("UrbanScan/Configure Project")]
        public static void ConfigureProject()
        {
            Debug.Log("[UrbanScan] === Начинаю настройку проекта ===");

            EnsureDirectories();
            var (pipelineAsset, rendererData) = CreateURPAssets();
            ConfigureGraphicsSettings(pipelineAsset);
            ConfigureQualitySettings(pipelineAsset);
            ConfigurePlayerSettings();
            ConfigureXRManagement();
            CreateMainScene(rendererData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[UrbanScan] === Настройка проекта завершена ===");
        }

        /// <summary>Создаём необходимые папки</summary>
        static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/Settings",
                "Assets/Scenes",
                "Assets/Prefabs",
                "Assets/Materials",
                "Assets/XR"
            };
            foreach (var dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parts = dir.Split('/');
                    var current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(current, parts[i]);
                        current = next;
                    }
                }
            }
        }

        /// <summary>Создаём URP Pipeline Asset и Forward Renderer</summary>
        static (UniversalRenderPipelineAsset, ScriptableRendererData) CreateURPAssets()
        {
            Debug.Log("[UrbanScan] Создаю URP ассеты...");

            // Создаём ForwardRendererData
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/ForwardRenderer.asset");

            // Создаём PipelineAsset с привязкой к рендереру
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, "Assets/Settings/URPAsset.asset");

            // Настройки URP для VR
            pipelineAsset.renderScale = 1.0f;
            pipelineAsset.msaaSampleCount = 4; // 4x MSAA — важно для VR
            pipelineAsset.supportsHDR = false;  // HDR не нужен, экономим GPU
            pipelineAsset.shadowDistance = 50f;

            EditorUtility.SetDirty(pipelineAsset);
            EditorUtility.SetDirty(rendererData);

            Debug.Log("[UrbanScan] URP ассеты созданы");
            return (pipelineAsset, rendererData);
        }

        /// <summary>Назначаем URP как основной рендер пайплайн</summary>
        static void ConfigureGraphicsSettings(UniversalRenderPipelineAsset pipelineAsset)
        {
            Debug.Log("[UrbanScan] Настраиваю Graphics Settings...");
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        }

        /// <summary>Настраиваем Quality Settings</summary>
        static void ConfigureQualitySettings(UniversalRenderPipelineAsset pipelineAsset)
        {
            Debug.Log("[UrbanScan] Настраиваю Quality Settings...");

            // Назначаем URP на все уровни качества
            var qualityLevels = QualitySettings.names;
            for (int i = 0; i < qualityLevels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            // Базовые настройки
            QualitySettings.vSyncCount = 0;          // VR сам управляет vsync
            QualitySettings.antiAliasing = 4;         // 4x MSAA
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
        }

        /// <summary>Настройки плеера (название, рендеринг, input)</summary>
        static void ConfigurePlayerSettings()
        {
            Debug.Log("[UrbanScan] Настраиваю Player Settings...");

            PlayerSettings.companyName = "iMax IT Company";
            PlayerSettings.productName = "UrbanScanVR";
            PlayerSettings.bundleVersion = "1.0.0";

            // Linear color space — обязательно для URP
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Scripting backend — Mono (для кросс-компиляции из Linux Docker)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);

            // API Compatibility — .NET Standard 2.1
            PlayerSettings.SetApiCompatibilityLevel(
                BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);

            // Input System: Both (новый + старый) для совместимости с XR Toolkit
            // Этот параметр устанавливается через ProjectSettings.asset напрямую
            SetActiveInputHandler(2); // 0=Old, 1=New, 2=Both

            // Fullscreen по умолчанию
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;

            // Разрешаем unsafe code (для Span<T> в парсере)
            PlayerSettings.allowUnsafeCode = true;
        }

        /// <summary>Настраиваем XR Management + OpenXR</summary>
        static void ConfigureXRManagement()
        {
            Debug.Log("[UrbanScan] Настраиваю XR Management...");

            // XR Management настраивается через EditorBuildSettings
            // и XRGeneralSettingsPerBuildTarget. Для batch mode используем
            // прямое создание ScriptableObject ассетов.

            // Создаём XR General Settings
            var generalSettings = ScriptableObject.CreateInstance<UnityEngine.XR.Management.XRGeneralSettings>();
            var managerSettings = ScriptableObject.CreateInstance<UnityEngine.XR.Management.XRManagerSettings>();

            generalSettings.Manager = managerSettings;

            // Добавляем OpenXR Loader
            var openXRLoader = ScriptableObject.CreateInstance<UnityEngine.XR.OpenXR.OpenXRLoader>();
            AssetDatabase.CreateAsset(openXRLoader, "Assets/XR/OpenXRLoader.asset");

            // Регистрируем лоадер
            managerSettings.TryAddLoader(openXRLoader);

            AssetDatabase.CreateAsset(managerSettings, "Assets/XR/XRManagerSettings.asset");
            AssetDatabase.CreateAsset(generalSettings, "Assets/XR/XRGeneralSettings.asset");

            // Настраиваем OpenXR — профили взаимодействия
            var openXRSettings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(
                BuildTargetGroup.Standalone);

            if (openXRSettings != null)
            {
                Debug.Log("[UrbanScan] OpenXR Settings настроены");
            }

            // Регистрируем в EditorBuildSettings
            UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
                managerSettings,
                typeof(UnityEngine.XR.OpenXR.OpenXRLoader).FullName,
                BuildTargetGroup.Standalone);

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(managerSettings);

            Debug.Log("[UrbanScan] XR Management настроен");
        }

        /// <summary>Создаём Main.unity сцену со всеми объектами</summary>
        static void CreateMainScene(ScriptableRendererData rendererData)
        {
            Debug.Log("[UrbanScan] Создаю Main.unity сцену...");

            // Новая пустая сцена
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // === XR Origin (VR игрок) ===
            var xrOriginGO = new GameObject("XR Origin (XR Rig)");

            // Camera Offset — дочерний объект для отслеживания высоты
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrOriginGO.transform);
            cameraOffset.transform.localPosition = Vector3.zero;

            // Main Camera
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(cameraOffset.transform);
            cameraGO.transform.localPosition = new Vector3(0, 1.7f, 0);

            var camera = cameraGO.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Skybox;

            // Добавляем UniversalAdditionalCameraData для URP
            var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;

            // TrackedPoseDriver для отслеживания головы
            cameraGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();

            // XR Origin компонент
            var xrOrigin = xrOriginGO.AddComponent<XROrigin>();
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            xrOrigin.Camera = camera;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // === Контроллеры ===
            CreateXRController(cameraOffset.transform, "Left Controller",
                UnityEngine.XR.InputDeviceCharacteristics.Left);
            CreateXRController(cameraOffset.transform, "Right Controller",
                UnityEngine.XR.InputDeviceCharacteristics.Right);

            // === XR Interaction Manager ===
            var interactionManagerGO = new GameObject("XR Interaction Manager");
            interactionManagerGO.AddComponent<XRInteractionManager>();

            // === Event System для VR UI ===
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<XRUIInputModule>();

            // === Освещение ===
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f); // тёплый белый
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ambient light
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);

            // === Game Manager (наши скрипты) ===
            var gameManagerGO = new GameObject("GameManager");

            // Добавляем наши MonoBehaviour скрипты по имени типа
            // (они будут доступны после компиляции всех скриптов)
            AddComponentByName(gameManagerGO, "UrbanScanVR.Bootstrap.AppBootstrap");
            AddComponentByName(gameManagerGO, "UrbanScanVR.Import.FileImportService");
            AddComponentByName(gameManagerGO, "UrbanScanVR.Scene.SceneSetupService");
            AddComponentByName(gameManagerGO, "UrbanScanVR.UI.UIManager");

            // PlayerController на XR Origin
            AddComponentByName(xrOriginGO, "UrbanScanVR.Player.PlayerController");

            // === Пол-плоскость (для начальной визуализации) ===
            var floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "Floor";
            floorGO.transform.localScale = new Vector3(10f, 1f, 10f);
            floorGO.transform.position = Vector3.zero;
            // Серый материал
            var floorRenderer = floorGO.GetComponent<MeshRenderer>();
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.35f));
            floorRenderer.material = floorMat;
            AssetDatabase.CreateAsset(floorMat, "Assets/Materials/FloorMaterial.asset");

            // Сохраняем сцену
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");

            // Добавляем в Build Settings
            var buildScenes = new EditorBuildSettingsScene[] {
                new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true)
            };
            EditorBuildSettings.scenes = buildScenes;

            Debug.Log("[UrbanScan] Main.unity сцена создана и добавлена в Build Settings");
        }

        /// <summary>Создаёт XR контроллер (левый или правый)</summary>
        static void CreateXRController(Transform parent, string name,
            UnityEngine.XR.InputDeviceCharacteristics hand)
        {
            var controllerGO = new GameObject(name);
            controllerGO.transform.SetParent(parent);
            controllerGO.transform.localPosition = Vector3.zero;

            // TrackedPoseDriver для отслеживания контроллера
            var poseDriver = controllerGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();

            // XR Controller для взаимодействий
            var controller = controllerGO.AddComponent<ActionBasedController>();

            // XR Ray Interactor для UI взаимодействия (лазерный указатель)
            var rayInteractor = controllerGO.AddComponent<XRRayInteractor>();
            var lineRenderer = controllerGO.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.005f;
            lineRenderer.endWidth = 0.005f;

            var lineVisual = controllerGO.AddComponent<XRInteractorLineVisual>();
        }

        /// <summary>Безопасное добавление компонента по полному имени типа</summary>
        static void AddComponentByName(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null)
            {
                go.AddComponent(type);
                Debug.Log($"[UrbanScan] Добавлен компонент: {typeName}");
            }
            else
            {
                Debug.LogWarning($"[UrbanScan] Тип не найден: {typeName} — будет добавлен вручную");
            }
        }

        /// <summary>Устанавливает Active Input Handler через ProjectSettings.asset</summary>
        /// <param name="value">0=Old, 1=New, 2=Both</param>
        static void SetActiveInputHandler(int value)
        {
            string settingsPath = "ProjectSettings/ProjectSettings.asset";
            string fullPath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), settingsPath);

            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogWarning("[UrbanScan] ProjectSettings.asset не найден, пропускаю настройку Input Handler");
                return;
            }

            string content = System.IO.File.ReadAllText(fullPath);

            // Ищем строку activeInputHandler и заменяем значение
            if (content.Contains("activeInputHandler:"))
            {
                content = System.Text.RegularExpressions.Regex.Replace(
                    content, @"activeInputHandler:\s*\d+",
                    $"activeInputHandler: {value}");
            }
            else
            {
                // Добавляем перед последней строкой
                content = content.TrimEnd() + $"\n  activeInputHandler: {value}\n";
            }

            System.IO.File.WriteAllText(fullPath, content);
            Debug.Log($"[UrbanScan] Active Input Handler установлен: {value} (Both)");
        }
    }
}
#endif
