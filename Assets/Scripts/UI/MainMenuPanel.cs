using UnityEngine;
using UnityEngine.UI;
using UrbanScanVR.Import;
using UrbanScanVR.Scene;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Главное меню VR: Импорт OBJ, Перезагрузить сцену, Выход.
    /// Индикатор статуса VR.
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        UIManager _uiManager;
        Text _titleText;
        Text _vrStatusText;
        GameObject _buttonsContainer;
        GameObject _vrNotConnectedContainer;

        public void Initialize(UIManager uiManager)
        {
            _uiManager = uiManager;
            BuildUI();
        }

        void BuildUI()
        {
            var parent = transform;

            // === Заголовок ===
            _titleText = UIManager.CreateText(parent, "Title", "UrbanScan VR",
                new Vector2(0, 280), 72, new Color(0.3f, 0.7f, 1f));

            // === Подзаголовок ===
            UIManager.CreateText(parent, "Subtitle", "Просмотр 3D-сцен из LiDAR сканов",
                new Vector2(0, 210), 36, new Color(0.7f, 0.7f, 0.7f));

            // === VR статус ===
            _vrStatusText = UIManager.CreateText(parent, "VRStatus", "VR: подключён",
                new Vector2(0, 155), 30, Color.green);

            // === Контейнер кнопок (нормальный режим) ===
            _buttonsContainer = new GameObject("Buttons");
            _buttonsContainer.transform.SetParent(parent, false);
            var buttonsRect = _buttonsContainer.AddComponent<RectTransform>();
            buttonsRect.anchorMin = Vector2.zero;
            buttonsRect.anchorMax = Vector2.one;
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            // Кнопка "Импорт OBJ"
            UIManager.CreateButton(_buttonsContainer.transform, "ImportButton",
                "Импорт OBJ", new Vector2(0, 50), OnImportClicked);

            // Кнопка "Перезагрузить сцену"
            UIManager.CreateButton(_buttonsContainer.transform, "ReloadButton",
                "Перезагрузить сцену", new Vector2(0, -50), OnReloadClicked);

            // Кнопка "Выход"
            UIManager.CreateButton(_buttonsContainer.transform, "ExitButton",
                "Выход", new Vector2(0, -150), OnExitClicked,
                new Vector2(300, 60));

            // === Контейнер "VR не подключен" ===
            _vrNotConnectedContainer = new GameObject("VRNotConnected");
            _vrNotConnectedContainer.transform.SetParent(parent, false);
            var nvrRect = _vrNotConnectedContainer.AddComponent<RectTransform>();
            nvrRect.anchorMin = Vector2.zero;
            nvrRect.anchorMax = Vector2.one;
            nvrRect.offsetMin = Vector2.zero;
            nvrRect.offsetMax = Vector2.zero;

            UIManager.CreateText(_vrNotConnectedContainer.transform, "ConnectMsg",
                "Подключите VR-шлем\nи нажмите \"Повторить\"",
                new Vector2(0, 50), 48, Color.white);

            UIManager.CreateButton(_vrNotConnectedContainer.transform, "RetryButton",
                "Повторить", new Vector2(0, -80), OnRetryVR);

            _vrNotConnectedContainer.SetActive(false);

            // === Нижняя строка ===
            UIManager.CreateText(parent, "Footer", "iMax IT Company  |  UrbanScan v1.0",
                new Vector2(0, -340), 24, new Color(0.4f, 0.4f, 0.4f));
        }

        /// <summary>Показать экран "VR не подключен"</summary>
        public void ShowVRNotConnected()
        {
            _buttonsContainer.SetActive(false);
            _vrNotConnectedContainer.SetActive(true);
            _vrStatusText.text = "VR: не подключён";
            _vrStatusText.color = Color.red;
        }

        /// <summary>Показать нормальное меню</summary>
        public void ShowNormalMenu()
        {
            _buttonsContainer.SetActive(true);
            _vrNotConnectedContainer.SetActive(false);
            _vrStatusText.text = "VR: подключён";
            _vrStatusText.color = Color.green;
        }

        // === Обработчики кнопок ===

        void OnImportClicked()
        {
            Debug.Log("[MainMenu] Импорт OBJ");

            var importService = FindAnyObjectByType<FileImportService>();
            if (importService == null)
            {
                _uiManager.ShowError("FileImportService не найден!");
                return;
            }

            // Подписываемся на события импорта
            importService.OnProgressChanged += OnImportProgress;
            importService.OnImportCompleted += OnImportDone;
            importService.OnImportFailed += OnImportError;

            // Показываем панель загрузки
            _uiManager.ShowLoading();

            // Запускаем импорт
            importService.ImportObjFile();
        }

        void OnImportProgress(float progress, string status)
        {
            _uiManager.UpdateProgress(progress, status);
        }

        void OnImportDone(GameObject model)
        {
            UnsubscribeImport();

            // Настраиваем сцену
            var sceneSetup = FindAnyObjectByType<SceneSetupService>();
            if (sceneSetup != null)
            {
                sceneSetup.SetupScene(model);
            }

            // Скрываем UI — пользователь в VR
            _uiManager.HideAll();
        }

        void OnImportError(string error)
        {
            UnsubscribeImport();
            _uiManager.ShowError(error);
        }

        void UnsubscribeImport()
        {
            var importService = FindAnyObjectByType<FileImportService>();
            if (importService == null) return;

            importService.OnProgressChanged -= OnImportProgress;
            importService.OnImportCompleted -= OnImportDone;
            importService.OnImportFailed -= OnImportError;
        }

        void OnReloadClicked()
        {
            Debug.Log("[MainMenu] Перезагрузка сцены");

            // Удаляем текущую модель
            var importService = FindAnyObjectByType<FileImportService>();
            if (importService?.CurrentModel != null)
            {
                Destroy(importService.CurrentModel);
            }

            // Удаляем коллайдер пола (если был создан SceneSetupService)
            var floorCollider = GameObject.Find("FloorCollider");
            if (floorCollider != null)
                Destroy(floorCollider);

            _uiManager.ShowMainMenu();
        }

        void OnExitClicked()
        {
            Debug.Log("[MainMenu] Выход");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnRetryVR()
        {
            Debug.Log("[MainMenu] Повторная попытка подключения VR");

            var bootstrap = FindAnyObjectByType<Bootstrap.AppBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.RetryVRConnection();
            }
        }
    }
}
