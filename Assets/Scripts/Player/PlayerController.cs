using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace UrbanScanVR.Player
{
    /// <summary>
    /// VR-перемещение: smooth locomotion (джойстик) + плавный поворот.
    /// Использует CharacterController для коллизий и гравитации.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Перемещение")]
        [SerializeField] float moveSpeed = 3f;
        [SerializeField] float sprintMultiplier = 2f;

        [Header("Поворот")]
        [SerializeField] float turnSpeed = 90f;       // градусов/сек
        [SerializeField] float turnDeadzone = 0.3f;    // мёртвая зона стика

        [Header("Физика")]
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float playerHeight = 1.8f;
        [SerializeField] float playerRadius = 0.3f;

        // Компоненты
        XROrigin _xrOrigin;
        CharacterController _characterController;

        // Input Actions
        InputAction _moveAction;
        InputAction _turnAction;
        InputAction _menuAction;

        // Состояние
        float _verticalVelocity;

        void Awake()
        {
            _xrOrigin = GetComponent<XROrigin>();

            // Создаём CharacterController для коллизий
            _characterController = gameObject.AddComponent<CharacterController>();
            _characterController.height = playerHeight;
            _characterController.radius = playerRadius;
            _characterController.center = new Vector3(0, playerHeight / 2f, 0);
            _characterController.stepOffset = 0.3f;
            _characterController.slopeLimit = 45f;
            _characterController.minMoveDistance = 0.001f;

            // Создаём Input Actions
            _moveAction = XRInputActions.CreateMoveAction();
            _turnAction = XRInputActions.CreateTurnAction();
            _menuAction = XRInputActions.CreateMenuAction();

            // Меню — по нажатию кнопки Menu
            _menuAction.performed += OnMenuPressed;
        }

        void Update()
        {
            HandleMovement();
            HandleTurn();
            ApplyGravity();
        }

        /// <summary>Перемещение по левому стику (относительно направления взгляда)</summary>
        void HandleMovement()
        {
            var input = _moveAction.ReadValue<Vector2>();

            if (input.sqrMagnitude < 0.01f)
                return;

            // Направление движения относительно камеры (головы)
            var cameraTransform = _xrOrigin.Camera.transform;
            var forward = cameraTransform.forward;
            var right = cameraTransform.right;

            // Проецируем на горизонтальную плоскость (убираем Y-компонент)
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // Итоговое направление
            var moveDirection = forward * input.y + right * input.x;

            // Применяем движение через CharacterController (с коллизиями)
            _characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
        }

        /// <summary>Плавный поворот по правому стику</summary>
        void HandleTurn()
        {
            var input = _turnAction.ReadValue<Vector2>();

            // Мёртвая зона — игнорируем мелкие отклонения
            if (Mathf.Abs(input.x) < turnDeadzone)
                return;

            float turnAmount = input.x * turnSpeed * Time.deltaTime;
            transform.Rotate(0f, turnAmount, 0f);
        }

        /// <summary>Гравитация — притягиваем к земле</summary>
        void ApplyGravity()
        {
            if (_characterController.isGrounded)
            {
                // На земле — небольшая прижимающая сила
                _verticalVelocity = -0.5f;
            }
            else
            {
                // В воздухе — ускорение свободного падения
                _verticalVelocity += gravity * Time.deltaTime;
            }

            _characterController.Move(new Vector3(0, _verticalVelocity * Time.deltaTime, 0));
        }

        void OnMenuPressed(InputAction.CallbackContext ctx)
        {
            // Переключаем меню (UIManager подпишется на это)
            Debug.Log("[PlayerController] Menu button pressed");

            // Ищем UIManager и переключаем меню
            var uiManager = FindAnyObjectByType<UI.UIManager>();
            if (uiManager != null)
            {
                uiManager.ToggleMenu();
            }
        }

        void OnDestroy()
        {
            // Освобождаем Input Actions
            _moveAction?.Disable();
            _moveAction?.Dispose();
            _turnAction?.Disable();
            _turnAction?.Dispose();
            _menuAction.performed -= OnMenuPressed;
            _menuAction?.Disable();
            _menuAction?.Dispose();
        }
    }
}
