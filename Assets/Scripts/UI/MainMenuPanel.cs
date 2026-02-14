using UnityEngine;
using UnityEngine.UI;
using UrbanScanVR.Import;
using UrbanScanVR.Scene;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Главное меню VR: Импорт OBJ, Перезагрузить сцену, Выход.
    /// Центральная карточка в glassmorphism-стиле.
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        UIManager _uiManager;
        Image _vrDot;
        Text _vrLabel;
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

            // === Центральная карточка ===
            var card = UIManager.CreateCard("MenuCard", parent, 700, 650,
                UIHelper.CardBg, UIHelper.Border);

            var cardT = card.transform;

            // === Заголовок ===
            UIManager.CreateText(cardT, "Title", "UrbanScan VR",
                new Vector2(0, 250), 64, UIHelper.TextPrimary);

            // === Подзаголовок ===
            UIManager.CreateText(cardT, "Subtitle", "3D LiDAR Viewer",
                new Vector2(0, 190), 30, UIHelper.TextSecondary);

            // === Разделитель ===
            UIManager.CreateSeparator(cardT, "Sep1", new Vector2(0, 155), 550f);

            // === VR статус ===
            var (dot, label) = UIManager.CreateStatusIndicator(cardT,
                "VRStatus", "VR Connected", new Vector2(0, 120), UIHelper.Success);
            _vrDot = dot;
            _vrLabel = label;

            // === Контейнер кнопок (нормальный режим) ===
            _buttonsContainer = new GameObject("Buttons");
            _buttonsContainer.transform.SetParent(cardT, false);
            var buttonsRect = _buttonsContainer.AddComponent<RectTransform>();
            buttonsRect.anchoredPosition = Vector2.zero;
            buttonsRect.sizeDelta = new Vector2(700, 400);

            // Кнопка "Импорт OBJ" — основная
            UIManager.CreateButton(_buttonsContainer.transform, "ImportButton",
                "Import OBJ", new Vector2(0, 40), OnImportClicked,
                new Vector2(500, 70), UIManager.ButtonStyle.Accent);

            // Кнопка "Перезагрузить сцену" — вторичная
            UIManager.CreateButton(_buttonsContainer.transform, "ReloadButton",
                "Reload Scene", new Vector2(0, -50), OnReloadClicked,
                new Vector2(500, 60), UIManager.ButtonStyle.Secondary);

            // Разделитель перед Exit
            UIManager.CreateSeparator(_buttonsContainer.transform, "Sep2",
                new Vector2(0, -110), 400f);

            // Кнопка "Выход" — danger
            UIManager.CreateButton(_buttonsContainer.transform, "ExitButton",
                "Exit", new Vector2(0, -160), OnExitClicked,
                new Vector2(200, 50), UIManager.ButtonStyle.Danger);

            // === Контейнер "VR не подключен" ===
            _vrNotConnectedContainer = new GameObject("VRNotConnected");
            _vrNotConnectedContainer.transform.SetParent(cardT, false);
            var nvrRect = _vrNotConnectedContainer.AddComponent<RectTransform>();
            nvrRect.anchoredPosition = Vector2.zero;
            nvrRect.sizeDelta = new Vector2(700, 400);

            UIManager.CreateText(_vrNotConnectedContainer.transform, "ConnectIcon",
                "!", new Vector2(0, 60), 72, UIHelper.Error);

            UIManager.CreateText(_vrNotConnectedContainer.transform, "ConnectMsg",
                "VR headset not connected\nPlease connect and retry",
                new Vector2(0, -20), 36, UIHelper.TextPrimary);

            UIManager.CreateButton(_vrNotConnectedContainer.transform, "RetryButton",
                "Retry Connection", new Vector2(0, -120), OnRetryVR,
                new Vector2(400, 65), UIManager.ButtonStyle.Accent);

            _vrNotConnectedContainer.SetActive(false);

            // === Футер ===
            UIManager.CreateText(cardT, "Footer", "iMax IT Company  |  UrbanScan v1.0",
                new Vector2(0, -290), 22, new Color(0.4f, 0.4f, 0.45f, 0.6f));
        }

        /// <summary>Показать экран "VR не подключен"</summary>
        public void ShowVRNotConnected()
        {
            _buttonsContainer.SetActive(false);
            _vrNotConnectedContainer.SetActive(true);
            _vrLabel.text = "VR Disconnected";
            _vrLabel.color = UIHelper.Error;
            _vrDot.sprite = UIHelper.CreateCircleSprite(32, UIHelper.Error);
        }

        /// <summary>Показать нормальное меню</summary>
        public void ShowNormalMenu()
        {
            _buttonsContainer.SetActive(true);
            _vrNotConnectedContainer.SetActive(false);
            _vrLabel.text = "VR Connected";
            _vrLabel.color = UIHelper.TextSecondary;
            _vrDot.sprite = UIHelper.CreateCircleSprite(32, UIHelper.Success);
        }

        // === Обработчики кнопок ===

        void OnImportClicked()
        {
            Debug.Log("[MainMenu] Import OBJ");

            var importService = FindAnyObjectByType<FileImportService>();
            if (importService == null)
            {
                _uiManager.ShowError("FileImportService not found!");
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
            Debug.Log("[MainMenu] Reload Scene");

            // Удаляем текущую модель
            var importService = FindAnyObjectByType<FileImportService>();
            if (importService?.CurrentModel != null)
            {
                Destroy(importService.CurrentModel);
            }

            // Удаляем коллайдер пола
            var floorCollider = GameObject.Find("FloorCollider");
            if (floorCollider != null)
                Destroy(floorCollider);

            _uiManager.ShowMainMenu();
        }

        void OnExitClicked()
        {
            Debug.Log("[MainMenu] Exit");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnRetryVR()
        {
            Debug.Log("[MainMenu] Retry VR connection");

            var bootstrap = FindAnyObjectByType<Bootstrap.AppBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.RetryVRConnection();
            }
        }
    }
}
