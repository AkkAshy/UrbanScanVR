using UnityEngine;
using UnityEngine.UI;

namespace UrbanScanVR.UI
{
    /// <summary>
    /// Панель загрузки OBJ: прогресс-бар + статус.
    /// Показывается во время импорта.
    /// </summary>
    public class LoadingPanel : MonoBehaviour
    {
        Image _progressFill;
        Text _statusText;
        Text _percentText;

        public void Initialize()
        {
            BuildUI();
        }

        void BuildUI()
        {
            var parent = transform;

            // === Заголовок ===
            UIManager.CreateText(parent, "LoadingTitle", "Загрузка модели...",
                new Vector2(0, 150), 56, new Color(0.3f, 0.7f, 1f));

            // === Прогресс-бар (фон) ===
            var barBG = new GameObject("ProgressBarBG");
            barBG.transform.SetParent(parent, false);
            var bgRect = barBG.AddComponent<RectTransform>();
            bgRect.anchoredPosition = new Vector2(0, 30);
            bgRect.sizeDelta = new Vector2(800, 40);
            var bgImage = barBG.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // === Прогресс-бар (заполнение) ===
            var barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barBG.transform, false);
            var fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1); // начинаем с нуля
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _progressFill = barFill.AddComponent<Image>();
            _progressFill.color = new Color(0.2f, 0.6f, 1f, 1f); // синий

            // === Процент ===
            _percentText = UIManager.CreateText(parent, "PercentText", "0%",
                new Vector2(0, -30), 42, Color.white);

            // === Статус ===
            _statusText = UIManager.CreateText(parent, "StatusText", "Подготовка...",
                new Vector2(0, -80), 32, new Color(0.6f, 0.6f, 0.7f));

            // === Подсказка ===
            UIManager.CreateText(parent, "Hint",
                "Выберите файл на мониторе компьютера",
                new Vector2(0, -160), 28, new Color(0.5f, 0.5f, 0.5f));
        }

        /// <summary>Обновить прогресс загрузки</summary>
        public void UpdateProgress(float progress, string status)
        {
            // Обновляем заполнение прогресс-бара через anchor
            if (_progressFill != null)
            {
                var rect = _progressFill.rectTransform;
                rect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            }

            // Процент
            if (_percentText != null)
                _percentText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            // Статус
            if (_statusText != null)
                _statusText.text = status;
        }

        /// <summary>Сброс прогресса</summary>
        public void Reset()
        {
            UpdateProgress(0, "Подготовка...");
        }

        void OnEnable()
        {
            Reset();
        }
    }
}
