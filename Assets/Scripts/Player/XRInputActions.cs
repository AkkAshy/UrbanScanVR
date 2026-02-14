using UnityEngine;
using UnityEngine.InputSystem;

namespace UrbanScanVR.Player
{
    /// <summary>
    /// Программное определение Input Actions для XR контроллеров.
    /// Заменяет .inputactions файл (его нельзя создать без Unity Editor).
    /// </summary>
    public static class XRInputActions
    {
        // Биндинги для OpenXR контроллеров
        const string LEFT_STICK = "<XRController>{LeftHand}/thumbstick";
        const string RIGHT_STICK = "<XRController>{RightHand}/thumbstick";
        const string LEFT_TRIGGER = "<XRController>{LeftHand}/trigger";
        const string RIGHT_TRIGGER = "<XRController>{RightHand}/trigger";
        const string LEFT_GRIP = "<XRController>{LeftHand}/grip";
        const string RIGHT_GRIP = "<XRController>{RightHand}/grip";
        const string MENU_BUTTON = "<XRController>{LeftHand}/menu";
        const string PRIMARY_BUTTON = "<XRController>{RightHand}/primaryButton"; // A/X

        /// <summary>Левый стик — перемещение (Vector2)</summary>
        public static InputAction CreateMoveAction()
        {
            var action = new InputAction("Move", InputActionType.Value);
            action.AddBinding(LEFT_STICK);
            action.Enable();
            return action;
        }

        /// <summary>Правый стик — поворот (Vector2)</summary>
        public static InputAction CreateTurnAction()
        {
            var action = new InputAction("Turn", InputActionType.Value);
            action.AddBinding(RIGHT_STICK);
            action.Enable();
            return action;
        }

        /// <summary>Правый триггер — взаимодействие с UI</summary>
        public static InputAction CreateUISelectAction()
        {
            var action = new InputAction("UISelect", InputActionType.Button);
            action.AddBinding(RIGHT_TRIGGER);
            action.Enable();
            return action;
        }

        /// <summary>Кнопка Menu — открыть/закрыть меню</summary>
        public static InputAction CreateMenuAction()
        {
            var action = new InputAction("Menu", InputActionType.Button);
            action.AddBinding(MENU_BUTTON);
            action.Enable();
            return action;
        }

        /// <summary>Кнопка A — подтверждение / доп. действие</summary>
        public static InputAction CreatePrimaryAction()
        {
            var action = new InputAction("Primary", InputActionType.Button);
            action.AddBinding(PRIMARY_BUTTON);
            action.Enable();
            return action;
        }
    }
}
