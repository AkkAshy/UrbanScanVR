using UnityEngine;
using UnityEngine.UI;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Панель загрузки OBJ: карточка с прогресс-баром и статусом.
    /// Скруглённый прогресс-бар + вращающийся индикатор.
    /// </summary>
    public class LoadingPanel : MonoBehaviour
    {
        Image _progressFill;
        Text _statusText;
        Text _percentText;
        RectTransform _spinner;
        float _spinAngle;

        public void Initialize()
        {
            BuildUI();
        }

        void BuildUI()
        {
            var parent = transform;

            // === Центральная карточка ===
            var card = UIManager.CreateCard("LoadingCard", parent, 600, 450,
                UIHelper.CardBg, UIHelper.Border);
            var cardT = card.transform;

            // === Вращающийся индикатор (кружок) ===
            var spinnerGO = new GameObject("Spinner");
            spinnerGO.transform.SetParent(cardT, false);
            _spinner = spinnerGO.AddComponent<RectTransform>();
            _spinner.anchoredPosition = new Vector2(0, 150);
            _spinner.sizeDelta = new Vector2(50, 50);

            // Дуга — используем частичный круг
            var spinnerImg = spinnerGO.AddComponent<Image>();
            spinnerImg.sprite = UIHelper.CreateCircleSprite(64, UIHelper.Accent);
            spinnerImg.color = new Color(1f, 1f, 1f, 0.8f);

            // Маленький внутренний круг (чтобы получился "бублик")
            var innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(spinnerGO.transform, false);
            var innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchoredPosition = Vector2.zero;
            innerRT.sizeDelta = new Vector2(34, 34);
            var innerImg = innerGO.AddComponent<Image>();
            innerImg.sprite = UIHelper.CreateCircleSprite(48, UIHelper.CardBg);

            // === Заголовок ===
            UIManager.CreateText(cardT, "LoadingTitle", "Loading Model...",
                new Vector2(0, 85), 48, UIHelper.Accent);

            // === Прогресс-бар (фон со скруглением) ===
            var barBgGO = UIManager.CreateCard("ProgressBarBG", cardT, 480, 32,
                new Color(0.15f, 0.15f, 0.22f, 1f),
                new Color(1f, 1f, 1f, 0.06f), 16);
            var barBgRT = barBgGO.GetComponent<RectTransform>();
            barBgRT.anchoredPosition = new Vector2(0, 20);

            // === Прогресс-бар (заполнение) ===
            var barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barBgGO.transform, false);
            var fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);

            _progressFill = barFill.AddComponent<Image>();
            _progressFill.sprite = UIHelper.CreateRoundedSprite(480, 28, 14,
                UIHelper.Accent, UIHelper.Accent, 0);
            _progressFill.type = Image.Type.Simple;

            // === Процент ===
            _percentText = UIManager.CreateText(cardT, "PercentText", "0%",
                new Vector2(0, -30), 52, UIHelper.TextPrimary);

            // === Статус ===
            _statusText = UIManager.CreateText(cardT, "StatusText", "Preparing...",
                new Vector2(0, -85), 30, UIHelper.TextSecondary);

            // === Подсказка ===
            UIManager.CreateText(cardT, "Hint",
                "Select a file on your computer",
                new Vector2(0, -150), 26, new Color(0.45f, 0.45f, 0.55f, 0.7f));
        }

        void Update()
        {
            // Анимация вращения спиннера
            if (_spinner != null)
            {
                _spinAngle -= 180f * Time.deltaTime;
                _spinner.localRotation = Quaternion.Euler(0, 0, _spinAngle);
            }
        }

        /// <summary>Обновить прогресс загрузки</summary>
        public void UpdateProgress(float progress, string status)
        {
            // Обновляем заполнение через anchor
            if (_progressFill != null)
            {
                var rect = _progressFill.rectTransform;
                rect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            }

            if (_percentText != null)
                _percentText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            if (_statusText != null)
                _statusText.text = status;
        }

        /// <summary>Сброс прогресса</summary>
        public void Reset()
        {
            UpdateProgress(0, "Preparing...");
            _spinAngle = 0;
        }

        void OnEnable()
        {
            Reset();
        }
    }
}
