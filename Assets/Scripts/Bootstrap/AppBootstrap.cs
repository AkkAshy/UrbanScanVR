using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

namespace UrbanScanVR.Bootstrap
{
    /// <summary>
    /// Точка входа приложения.
    /// Проверяет VR-шлем, инициализирует XR, показывает главное меню.
    /// Выполняется раньше других скриптов.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AppBootstrap : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Интервал опроса VR-подключения (сек)")]
        [SerializeField] float vrPollInterval = 2f;

        Coroutine _vrPollCoroutine;

        void Start()
        {
            Debug.Log("[AppBootstrap] === UrbanScanVR запуск ===");
            Application.targetFrameRate = 90; // для VR

            InitializeXR();
        }

        /// <summary>Инициализация XR подсистемы</summary>
        void InitializeXR()
        {
            var xrSettings = XRGeneralSettings.Instance;

            if (xrSettings == null)
            {
                Debug.LogError("[AppBootstrap] XRGeneralSettings не настроен!");
                ShowVRNotFound();
                return;
            }

            var manager = xrSettings.Manager;
            if (manager == null)
            {
                Debug.LogError("[AppBootstrap] XRManagerSettings не найден!");
                ShowVRNotFound();
                return;
            }

            // Пробуем инициализировать XR Loader
            manager.InitializeLoaderSync();

            if (manager.activeLoader == null)
            {
                // VR-шлем не подключен
                Debug.LogWarning("[AppBootstrap] VR-шлем не найден. Ожидаю подключения...");
                ShowVRNotFound();
                return;
            }

            // XR успешно инициализирован — запускаем подсистемы
            manager.StartSubsystems();
            Debug.Log("[AppBootstrap] XR инициализирован: " + manager.activeLoader.GetType().Name);

            // Показываем главное меню
            ShowMainMenu();
        }

        /// <summary>Показать экран "Подключите VR-шлем"</summary>
        void ShowVRNotFound()
        {
            var uiManager = FindAnyObjectByType<UI.UIManager>();
            if (uiManager != null)
            {
                uiManager.ShowConnectVRScreen();
            }

            // Запускаем опрос VR-подключения
            if (_vrPollCoroutine == null)
            {
                _vrPollCoroutine = StartCoroutine(PollForVRConnection());
            }
        }

        /// <summary>Показать главное меню</summary>
        void ShowMainMenu()
        {
            var uiManager = FindAnyObjectByType<UI.UIManager>();
            if (uiManager != null)
            {
                uiManager.SwitchToWorldSpace();
                uiManager.ShowMainMenu();
            }
        }

        /// <summary>Периодическая проверка подключения VR-шлема</summary>
        IEnumerator PollForVRConnection()
        {
            while (true)
            {
                yield return new WaitForSeconds(vrPollInterval);

                var xrSettings = XRGeneralSettings.Instance;
                if (xrSettings?.Manager == null) continue;

                // Если уже инициализирован — выходим
                if (xrSettings.Manager.activeLoader != null)
                {
                    OnVRConnected();
                    yield break;
                }
            }
        }

        /// <summary>
        /// Повторная попытка подключения VR (вызывается из UI).
        /// </summary>
        public void RetryVRConnection()
        {
            Debug.Log("[AppBootstrap] Повторная попытка подключения VR...");

            var xrSettings = XRGeneralSettings.Instance;
            if (xrSettings?.Manager == null) return;

            // Пробуем инициализировать снова
            xrSettings.Manager.InitializeLoaderSync();

            if (xrSettings.Manager.activeLoader != null)
            {
                OnVRConnected();
            }
            else
            {
                Debug.LogWarning("[AppBootstrap] VR-шлем всё ещё не подключен");
            }
        }

        /// <summary>VR-шлем подключен!</summary>
        void OnVRConnected()
        {
            Debug.Log("[AppBootstrap] VR-шлем подключен!");

            // Останавливаем опрос
            if (_vrPollCoroutine != null)
            {
                StopCoroutine(_vrPollCoroutine);
                _vrPollCoroutine = null;
            }

            // Запускаем подсистемы
            var manager = XRGeneralSettings.Instance.Manager;
            manager.StartSubsystems();

            // Показываем меню
            ShowMainMenu();
        }

        void OnDestroy()
        {
            // Корректно останавливаем XR
            var xrSettings = XRGeneralSettings.Instance;
            if (xrSettings?.Manager != null)
            {
                xrSettings.Manager.StopSubsystems();
                xrSettings.Manager.DeinitializeLoader();
                Debug.Log("[AppBootstrap] XR подсистемы остановлены");
            }
        }
    }
}
