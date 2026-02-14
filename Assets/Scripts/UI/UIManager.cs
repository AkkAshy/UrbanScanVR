using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Управление VR UI.
    /// Создаёт World Space Canvas и все панели программно.
    /// Canvas следует за игроком и управляется кнопкой Menu.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // === Панели ===
        MainMenuPanel _mainMenu;
        LoadingPanel _loadingPanel;
        GameObject _errorPanel;
        GameObject _vrStatusPanel;

        // === Компоненты ===
        Canvas _canvas;
        GameObject _canvasGO;

        // === Настройки ===
        const float CANVAS_DISTANCE = 2.5f;   // метры перед игроком
        const float CANVAS_HEIGHT = 1.5f;      // метры от пола
        const float CANVAS_SCALE = 0.001f;     // масштаб (1 pixel = 1mm)
        const int CANVAS_WIDTH = 1200;
        const int CANVAS_HEIGHT_PX = 800;

        bool _menuVisible = true;

        void Start()
        {
            CreateCanvas();
            CreateMainMenu();
            CreateLoadingPanel();
            CreateErrorPanel();

            // Показываем меню по умолчанию
            ShowMainMenu();
        }

        /// <summary>Создаёт World Space Canvas для VR</summary>
        void CreateCanvas()
        {
            _canvasGO = new GameObject("VR UI Canvas");
            _canvasGO.transform.SetParent(transform);

            // Canvas в World Space
            _canvas = _canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            // Масштаб: 1 pixel = 1mm
            _canvasGO.transform.localScale = Vector3.one * CANVAS_SCALE;

            // Размер Canvas
            var rectTransform = _canvasGO.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT_PX);

            // Canvas Scaler
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            // Graphic Raycaster для XR взаимодействия
            _canvasGO.AddComponent<GraphicRaycaster>();

            // Позиционируем перед игроком
            PositionCanvasInFrontOfPlayer();
        }

        /// <summary>Главное меню: Импорт OBJ, Перезагрузить, Выход</summary>
        void CreateMainMenu()
        {
            var panel = CreatePanel("MainMenu", _canvasGO.transform);
            _mainMenu = panel.AddComponent<MainMenuPanel>();
            _mainMenu.Initialize(this);
        }

        /// <summary>Панель загрузки с прогресс-баром</summary>
        void CreateLoadingPanel()
        {
            var panel = CreatePanel("LoadingPanel", _canvasGO.transform);
            _loadingPanel = panel.AddComponent<LoadingPanel>();
            _loadingPanel.Initialize();
            panel.SetActive(false);
        }

        /// <summary>Панель ошибок</summary>
        void CreateErrorPanel()
        {
            _errorPanel = CreatePanel("ErrorPanel", _canvasGO.transform);

            // Заголовок
            CreateText(_errorPanel.transform, "ErrorTitle", "Ошибка",
                new Vector2(0, 100), 36, Color.red);

            // Текст ошибки
            CreateText(_errorPanel.transform, "ErrorMessage", "",
                new Vector2(0, 0), 24, Color.white);

            // Кнопка ОК
            CreateButton(_errorPanel.transform, "OKButton", "OK",
                new Vector2(0, -120), () =>
                {
                    _errorPanel.SetActive(false);
                    ShowMainMenu();
                });

            _errorPanel.SetActive(false);
        }

        // === Публичные методы ===

        /// <summary>Показать главное меню</summary>
        public void ShowMainMenu()
        {
            _mainMenu.gameObject.SetActive(true);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(false);
            _menuVisible = true;
            PositionCanvasInFrontOfPlayer();
        }

        /// <summary>Показать панель загрузки</summary>
        public void ShowLoading()
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(true);
            _errorPanel.SetActive(false);
            _menuVisible = true;
        }

        /// <summary>Обновить прогресс загрузки</summary>
        public void UpdateProgress(float progress, string status)
        {
            _loadingPanel.UpdateProgress(progress, status);
        }

        /// <summary>Показать ошибку</summary>
        public void ShowError(string message)
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(true);

            var errorText = _errorPanel.transform.Find("ErrorMessage")?.GetComponent<Text>();
            if (errorText != null)
                errorText.text = message;
        }

        /// <summary>Скрыть всё UI</summary>
        public void HideAll()
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(false);
            _menuVisible = false;
        }

        /// <summary>Переключить видимость меню (кнопка Menu на контроллере)</summary>
        public void ToggleMenu()
        {
            if (_menuVisible)
            {
                HideAll();
            }
            else
            {
                ShowMainMenu();
            }
        }

        /// <summary>Показать экран "Подключите VR-шлем"</summary>
        public void ShowConnectVRScreen()
        {
            HideAll();

            // Для экрана без VR переключаем canvas в Screen Space
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGO.transform.localScale = Vector3.one;

            _mainMenu.gameObject.SetActive(true);
            _mainMenu.ShowVRNotConnected();
        }

        /// <summary>Переключить canvas обратно в World Space (после подключения VR)</summary>
        public void SwitchToWorldSpace()
        {
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvasGO.transform.localScale = Vector3.one * CANVAS_SCALE;
            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT_PX);
        }

        // === Утилиты для создания UI элементов ===

        /// <summary>Ставим Canvas перед лицом игрока</summary>
        void PositionCanvasInFrontOfPlayer()
        {
            var xrOrigin = FindAnyObjectByType<XROrigin>();
            if (xrOrigin == null || xrOrigin.Camera == null) return;

            var cam = xrOrigin.Camera.transform;
            var forward = cam.forward;
            forward.y = 0;
            forward.Normalize();

            _canvasGO.transform.position = cam.position
                + forward * CANVAS_DISTANCE
                + Vector3.up * (CANVAS_HEIGHT - cam.position.y);

            _canvasGO.transform.rotation = Quaternion.LookRotation(forward);
        }

        /// <summary>Создаёт панель с полупрозрачным фоном</summary>
        public static GameObject CreatePanel(string name, Transform parent)
        {
            var panelGO = new GameObject(name);
            panelGO.transform.SetParent(parent, false);

            var rect = panelGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Полупрозрачный тёмный фон
            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

            return panelGO;
        }

        /// <summary>Создаёт UI Text</summary>
        public static Text CreateText(Transform parent, string name, string text,
            Vector2 position, int fontSize, Color color)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1000, 100);

            var uiText = textGO.AddComponent<Text>();
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            // Используем встроенный шрифт Arial
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiText.font == null)
                uiText.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

            return uiText;
        }

        /// <summary>Создаёт кнопку с текстом</summary>
        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
        {
            var btnSize = size ?? new Vector2(400, 80);

            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);

            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = btnSize;

            // Фон кнопки
            var image = btnGO.AddComponent<Image>();
            image.color = new Color(0.15f, 0.4f, 0.9f, 1f); // синий

            // Button компонент
            var button = btnGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.5f, 1f, 1f);
            colors.pressedColor = new Color(0.1f, 0.3f, 0.7f, 1f);
            button.colors = colors;

            button.onClick.AddListener(onClick);

            // Текст на кнопке
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(btnGO.transform, false);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var uiText = textGO.AddComponent<Text>();
            uiText.text = label;
            uiText.fontSize = 28;
            uiText.color = Color.white;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiText.font == null)
                uiText.font = Font.CreateDynamicFontFromOSFont("Arial", 28);

            return button;
        }
    }
}
