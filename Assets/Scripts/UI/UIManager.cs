using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Управление VR UI.
    /// Создаёт World Space Canvas и все панели программно.
    /// Современный glassmorphism-стиль с процедурными текстурами.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // === Панели ===
        MainMenuPanel _mainMenu;
        LoadingPanel _loadingPanel;
        GameObject _errorPanel;

        // === Компоненты ===
        Canvas _canvas;
        GameObject _canvasGO;

        // === Настройки ===
        const float CANVAS_DISTANCE = 2.0f;
        const float CANVAS_HEIGHT = 1.5f;
        const float CANVAS_SCALE = 0.002f;
        const int CANVAS_WIDTH = 1200;
        const int CANVAS_HEIGHT_PX = 800;

        bool _menuVisible = true;

        void Start()
        {
            CreateCanvas();
            CreateMainMenu();
            CreateLoadingPanel();
            CreateErrorPanel();
            ShowMainMenu();
        }

        // ============================================================
        // Canvas
        // ============================================================

        void CreateCanvas()
        {
            _canvasGO = new GameObject("VR UI Canvas");
            _canvasGO.transform.SetParent(transform);

            _canvas = _canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvasGO.transform.localScale = Vector3.one * CANVAS_SCALE;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT_PX);

            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            _canvasGO.AddComponent<GraphicRaycaster>();
            CreateBackground();
            PositionCanvasInFrontOfPlayer();
        }

        /// <summary>Фоновое изображение на весь Canvas из Resources/background</summary>
        void CreateBackground()
        {
            var bgTex = Resources.Load<Texture2D>("background");
            if (bgTex == null) return;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_canvasGO.transform, false);

            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = Sprite.Create(bgTex,
                new Rect(0, 0, bgTex.width, bgTex.height),
                new Vector2(0.5f, 0.5f));
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;

            // Затемняем чтобы карточка и текст читались
            bgImg.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            bgImg.raycastTarget = false;
            bgGO.transform.SetAsFirstSibling();
        }

        // ============================================================
        // Панели
        // ============================================================

        void CreateMainMenu()
        {
            // Прозрачный контейнер на весь Canvas
            var container = CreateTransparentContainer("MainMenu", _canvasGO.transform);
            _mainMenu = container.AddComponent<MainMenuPanel>();
            _mainMenu.Initialize(this);
        }

        void CreateLoadingPanel()
        {
            var container = CreateTransparentContainer("LoadingPanel", _canvasGO.transform);
            _loadingPanel = container.AddComponent<LoadingPanel>();
            _loadingPanel.Initialize();
            container.SetActive(false);
        }

        void CreateErrorPanel()
        {
            _errorPanel = CreateTransparentContainer("ErrorPanel", _canvasGO.transform);

            // Карточка
            var card = CreateCard("ErrorCard", _errorPanel.transform, 600, 400,
                UIHelper.CardBg, new Color(1f, 0.3f, 0.3f, 0.3f));

            // Иконка ошибки
            CreateText(card.transform, "ErrorIcon", "!",
                new Vector2(0, 120), 80, UIHelper.Error);

            CreateText(card.transform, "ErrorTitle", "Error",
                new Vector2(0, 50), 42, UIHelper.TextPrimary);

            CreateText(card.transform, "ErrorMessage", "",
                new Vector2(0, -30), 28, UIHelper.TextSecondary);

            CreateButton(card.transform, "OKButton", "OK",
                new Vector2(0, -130), () =>
                {
                    _errorPanel.SetActive(false);
                    ShowMainMenu();
                }, new Vector2(200, 55), ButtonStyle.Accent);

            _errorPanel.SetActive(false);
        }

        // ============================================================
        // Публичные методы
        // ============================================================

        public void ShowMainMenu()
        {
            _mainMenu.gameObject.SetActive(true);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(false);
            _menuVisible = true;
            PositionCanvasInFrontOfPlayer();
        }

        public void ShowLoading()
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(true);
            _errorPanel.SetActive(false);
            _menuVisible = true;
        }

        public void UpdateProgress(float progress, string status)
        {
            _loadingPanel.UpdateProgress(progress, status);
        }

        public void ShowError(string message)
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(true);

            var errorText = _errorPanel.transform.Find("ErrorCard/ErrorMessage")?.GetComponent<Text>();
            if (errorText != null)
                errorText.text = message;
        }

        public void HideAll()
        {
            _mainMenu.gameObject.SetActive(false);
            _loadingPanel.gameObject.SetActive(false);
            _errorPanel.SetActive(false);
            _menuVisible = false;
        }

        public void ToggleMenu()
        {
            if (_menuVisible) HideAll();
            else ShowMainMenu();
        }

        public void ShowConnectVRScreen()
        {
            HideAll();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGO.transform.localScale = Vector3.one;
            _mainMenu.gameObject.SetActive(true);
            _mainMenu.ShowVRNotConnected();
        }

        public void SwitchToWorldSpace()
        {
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvasGO.transform.localScale = Vector3.one * CANVAS_SCALE;
            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT_PX);
        }

        // ============================================================
        // Утилиты позиционирования
        // ============================================================

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

        // ============================================================
        // UI Factory — создание элементов
        // ============================================================

        /// <summary>Прозрачный контейнер на весь Canvas (без фона)</summary>
        public static GameObject CreateTransparentContainer(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Карточка со скруглёнными углами и рамкой</summary>
        public static GameObject CreateCard(string name, Transform parent,
            int width, int height, Color fill, Color border, int radius = 24)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);

            var img = go.AddComponent<Image>();
            img.sprite = UIHelper.CreateRoundedSprite(width, height, radius, fill, border, 2);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;

            return go;
        }

        /// <summary>UI Text</summary>
        public static Text CreateText(Transform parent, string name, string text,
            Vector2 position, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(1000, fontSize + 40);

            var uiText = go.AddComponent<Text>();
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiText.font == null)
                uiText.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

            return uiText;
        }

        // === Стили кнопок ===
        public enum ButtonStyle { Accent, Secondary, Danger }

        /// <summary>Кнопка со скруглёнными углами и стилем</summary>
        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 position, UnityEngine.Events.UnityAction onClick,
            Vector2? size = null, ButtonStyle style = ButtonStyle.Accent)
        {
            var btnSize = size ?? new Vector2(500, 65);
            int w = (int)btnSize.x;
            int h = (int)btnSize.y;

            // Определяем цвета по стилю
            Color topColor, bottomColor, borderColor, hoverColor, pressColor, textColor;
            switch (style)
            {
                case ButtonStyle.Secondary:
                    topColor = UIHelper.Secondary;
                    bottomColor = UIHelper.Secondary;
                    borderColor = UIHelper.SecondaryBorder;
                    hoverColor = new Color(0.25f, 0.25f, 0.38f, 0.95f);
                    pressColor = new Color(0.15f, 0.15f, 0.25f, 0.95f);
                    textColor = UIHelper.TextPrimary;
                    break;
                case ButtonStyle.Danger:
                    topColor = UIHelper.Danger;
                    bottomColor = UIHelper.DangerDark;
                    borderColor = new Color(1f, 0.3f, 0.3f, 0.2f);
                    hoverColor = new Color(0.9f, 0.25f, 0.25f, 1f);
                    pressColor = new Color(0.6f, 0.15f, 0.15f, 1f);
                    textColor = UIHelper.TextPrimary;
                    break;
                default: // Accent
                    topColor = UIHelper.Accent;
                    bottomColor = UIHelper.AccentDark;
                    borderColor = UIHelper.BorderLight;
                    hoverColor = UIHelper.AccentHover;
                    pressColor = new Color(0.12f, 0.30f, 0.70f, 1f);
                    textColor = Color.white;
                    break;
            }

            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);

            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = btnSize;

            var img = btnGO.AddComponent<Image>();
            img.sprite = UIHelper.CreateGradientSprite(w, h, 12, topColor, bottomColor, borderColor, 2);
            img.type = Image.Type.Simple;

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            // Текст
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);

            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(10, 0);
            labelRT.offsetMax = new Vector2(-10, 0);

            var uiText = labelGO.AddComponent<Text>();
            uiText.text = label;
            uiText.fontSize = Mathf.Clamp(h / 2, 20, 40);
            uiText.color = textColor;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiText.font == null)
                uiText.font = Font.CreateDynamicFontFromOSFont("Arial", 32);

            return btn;
        }

        /// <summary>Разделитель — горизонтальная линия</summary>
        public static GameObject CreateSeparator(Transform parent, string name,
            Vector2 position, float width = 500f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 1);

            var img = go.AddComponent<Image>();
            img.color = UIHelper.Border;

            return go;
        }

        /// <summary>Индикатор статуса: кружок + текст</summary>
        public static (Image dot, Text label) CreateStatusIndicator(Transform parent,
            string name, string text, Vector2 position, Color dotColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(300, 36);

            // Кружок
            var dotGO = new GameObject("Dot");
            dotGO.transform.SetParent(go.transform, false);

            var dotRT = dotGO.AddComponent<RectTransform>();
            dotRT.anchoredPosition = new Vector2(-100, 0);
            dotRT.sizeDelta = new Vector2(18, 18);

            var dotImg = dotGO.AddComponent<Image>();
            dotImg.sprite = UIHelper.CreateCircleSprite(32, dotColor);

            // Текст
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);

            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchoredPosition = new Vector2(15, 0);
            labelRT.sizeDelta = new Vector2(250, 36);

            var uiText = labelGO.AddComponent<Text>();
            uiText.text = text;
            uiText.fontSize = 26;
            uiText.color = UIHelper.TextSecondary;
            uiText.alignment = TextAnchor.MiddleLeft;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiText.font == null)
                uiText.font = Font.CreateDynamicFontFromOSFont("Arial", 26);

            return (dotImg, uiText);
        }
    }
}
