using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Unity.MP_FPS
{
    using UnityCharacterController = UnityEngine.CharacterController;
    using UnityPlayerInput = UnityEngine.InputSystem.PlayerInput;

    /// <summary>
    /// Standalone First-Person Controller handling movement, jumping, mouse look,
    /// sprinting, and Cinemachine 3.x FPS camera integration using Unity's New Input System.
    /// Exposes decoupled events for camera FOV and VFX systems.
    /// </summary>
    [RequireComponent(typeof(UnityCharacterController))]
    public class FPSMovementController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Standard walking speed in meters per second.")]
        [SerializeField] private float m_WalkSpeed = 6.0f;

        [Tooltip("Sprint speed in meters per second when Shift is held.")]
        [SerializeField] private float m_SprintSpeed = 10.0f;

        [Tooltip("Rate of acceleration/deceleration between walking and sprinting speeds.")]
        [SerializeField] private float m_SprintAcceleration = 12.0f;

        [Tooltip("If true, sprinting is only allowed when moving forward.")]
        [SerializeField] private bool m_SprintOnlyWhenMovingForward = true;

        [Tooltip("Maximum jump apex height in meters.")]
        [SerializeField] private float m_JumpHeight = 1.2f;

        [Tooltip("Custom gravity acceleration in meters per second squared.")]
        [SerializeField] private float m_Gravity = -19.62f;

        [Header("Look Settings")]
        [Tooltip("Mouse look sensitivity multiplier.")]
        [SerializeField] private float m_LookSensitivity = 0.12f;

        [Tooltip("Minimum vertical look angle (degrees, looking down).")]
        [SerializeField] private float m_PitchClampMin = -85.0f;

        [Tooltip("Maximum vertical look angle (degrees, looking up).")]
        [SerializeField] private float m_PitchClampMax = 85.0f;

        [Header("Cursor Settings")]
        [Tooltip("Whether to lock the cursor to screen center automatically on start.")]
        [SerializeField] private bool m_LockCursorOnStart = true;

        [Header("References")]
        [Tooltip("Transform representing the eye/camera pivot for vertical pitch rotation.")]
        [SerializeField] private Transform m_CameraRoot;

        [Tooltip("CinemachineCamera component attached to or tracking the camera root.")]
        [SerializeField] private CinemachineCamera m_CinemachineCamera;

        [Tooltip("CharacterController driving physical movement and collision.")]
        [SerializeField] private UnityCharacterController m_CharacterController;

        [Tooltip("Optional PlayerInput component referencing InputSystem_Actions.")]
        [SerializeField] private UnityPlayerInput m_PlayerInput;

        // Decoupled Sprint Events for Camera FOV & VFX systems
        public event Action<bool> OnSprintStateChanged;
        public event Action<float> OnSprintProgress;

        // Public Read-Only State
        public bool IsSprinting { get; private set; }
        public float SprintProgress { get; private set; }
        public float CurrentSpeed => m_CurrentSpeed;
        public float WalkSpeed => m_WalkSpeed;
        public float SprintSpeed => m_SprintSpeed;
        public Transform CameraRoot => m_CameraRoot;
        public CinemachineCamera CinemachineCam => m_CinemachineCamera;
        public UnityCharacterController Controller => m_CharacterController;

        // Internal state
        private float m_CurrentSpeed;
        private float m_CameraPitch;
        private float m_VerticalVelocity;
        private InputAction m_MoveAction;
        private InputAction m_LookAction;
        private InputAction m_JumpAction;
        private InputAction m_SprintAction;
        private InputSystem_Actions m_InternalActions;

        private void Awake()
        {
            m_CurrentSpeed = m_WalkSpeed;

            if (m_CharacterController == null)
            {
                m_CharacterController = GetComponent<UnityCharacterController>();
            }

            if (m_CameraRoot == null)
            {
                var found = transform.Find("CameraRoot");
                if (found != null)
                {
                    m_CameraRoot = found;
                }
            }

            if (m_CinemachineCamera == null)
            {
                m_CinemachineCamera = GetComponentInChildren<CinemachineCamera>();
            }

            // Set up input actions
            SetupInputActions();
        }

        private void SetupInputActions()
        {
            if (m_PlayerInput == null)
            {
                m_PlayerInput = GetComponent<UnityPlayerInput>();
            }

            if (m_PlayerInput != null && m_PlayerInput.actions != null)
            {
                m_MoveAction = m_PlayerInput.actions["Move"];
                m_LookAction = m_PlayerInput.actions["Look"];
                m_JumpAction = m_PlayerInput.actions["Jump"];
                m_SprintAction = m_PlayerInput.actions["Sprint"];
            }
            else
            {
                // Fallback to internal generated InputSystem_Actions
                m_InternalActions = new InputSystem_Actions();
                m_MoveAction = m_InternalActions.Player.Move;
                m_LookAction = m_InternalActions.Player.Look;
                m_JumpAction = m_InternalActions.Player.Jump;
                m_SprintAction = m_InternalActions.Player.Sprint;
                m_InternalActions.Enable();
            }
        }

        private void Start()
        {
            if (m_LockCursorOnStart)
            {
                SetCursorLock(true);
            }

            // Ensure CinemachineCamera follows and looks at CameraRoot if assigned
            if (m_CinemachineCamera != null && m_CameraRoot != null)
            {
                if (m_CinemachineCamera.Follow == null)
                {
                    m_CinemachineCamera.Follow = m_CameraRoot;
                }
            }
        }

        private void OnEnable()
        {
            if (m_InternalActions != null)
            {
                m_InternalActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (m_InternalActions != null)
            {
                m_InternalActions.Disable();
            }
        }

        private void OnDestroy()
        {
            if (m_InternalActions != null)
            {
                m_InternalActions.Dispose();
                m_InternalActions = null;
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLook();
            HandleMovementAndJump();
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(false);
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLock(true);
            }
        }

        private void SetCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 lookDelta = m_LookAction != null ? m_LookAction.ReadValue<Vector2>() : Vector2.zero;
            float mouseX = lookDelta.x * m_LookSensitivity;
            float mouseY = lookDelta.y * m_LookSensitivity;

            // Vertical pitch (camera only)
            m_CameraPitch -= mouseY;
            m_CameraPitch = Mathf.Clamp(m_CameraPitch, m_PitchClampMin, m_PitchClampMax);

            if (m_CameraRoot != null)
            {
                m_CameraRoot.localRotation = Quaternion.Euler(m_CameraPitch, 0.0f, 0.0f);
            }

            // Horizontal yaw (entire player body)
            transform.Rotate(Vector3.up * mouseX);
        }

        private void HandleMovementAndJump()
        {
            bool isGrounded = m_CharacterController.isGrounded;

            if (isGrounded && m_VerticalVelocity < 0.0f)
            {
                // Small grounding force to avoid jitter on slopes
                m_VerticalVelocity = -2.0f;
            }

            // Movement input
            Vector2 moveInput = m_MoveAction != null ? m_MoveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (moveDirection.sqrMagnitude > 1.0f)
            {
                moveDirection.Normalize();
            }

            // Sprint detection (Shift held)
            bool sprintInput = false;
            if (m_SprintAction != null)
            {
                sprintInput = m_SprintAction.IsPressed();
            }
            else if (Keyboard.current != null)
            {
                sprintInput = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }

            // Determine if sprint can be activated
            bool isMoving = moveDirection.sqrMagnitude > 0.01f;
            bool forwardConditionMet = !m_SprintOnlyWhenMovingForward || moveInput.y > 0.1f;
            bool wantsToSprint = sprintInput && isMoving && forwardConditionMet && isGrounded;

            // Update sprinting state and dispatch state change event
            if (wantsToSprint != IsSprinting)
            {
                IsSprinting = wantsToSprint;
                OnSprintStateChanged?.Invoke(IsSprinting);
            }

            // Target speed and smooth acceleration
            float targetSpeed = IsSprinting ? m_SprintSpeed : m_WalkSpeed;
            m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, targetSpeed, m_SprintAcceleration * Time.deltaTime);

            // Calculate normalized sprint progress (0.0 to 1.0)
            float speedRange = Mathf.Max(0.01f, m_SprintSpeed - m_WalkSpeed);
            SprintProgress = Mathf.Clamp01((m_CurrentSpeed - m_WalkSpeed) / speedRange);
            OnSprintProgress?.Invoke(SprintProgress);

            // Jump
            bool jumpTriggered = m_JumpAction != null && m_JumpAction.WasPressedThisFrame();
            if (jumpTriggered && isGrounded)
            {
                m_VerticalVelocity = Mathf.Sqrt(m_JumpHeight * -2.0f * m_Gravity);
            }

            // Apply gravity
            m_VerticalVelocity += m_Gravity * Time.deltaTime;

            // Final motion
            Vector3 motion = (moveDirection * m_CurrentSpeed) + (Vector3.up * m_VerticalVelocity);
            m_CharacterController.Move(motion * Time.deltaTime);
        }
    }
}
