using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

namespace Unity.MP_FPS
{
    /// <summary>
    /// Interface for any VFX, post-processing, or audio module that reacts to sprint intensity (0.0 to 1.0).
    /// Allows plugging in speed lines, motion blur, particle trails, or sound effects without modifying camera or movement code.
    /// </summary>
    public interface ISprintVFXReceiver
    {
        void SetSprintVFXIntensity(float intensity);
    }

    /// <summary>
    /// Handles first-person camera dynamic FOV changes when sprinting and dispatches
    /// normalized sprint intensity to scalable VFX receivers and inspector events.
    /// </summary>
    [DisallowMultipleComponent]
    public class FPSCameraEffects : MonoBehaviour
    {
        [Header("Cinemachine References")]
        [Tooltip("The CinemachineCamera whose Lens FOV will be dynamically adjusted.")]
        [SerializeField] private CinemachineCamera m_CinemachineCamera;

        [Tooltip("The movement controller providing sprint state and progress.")]
        [SerializeField] private FPSMovementController m_MovementController;

        [Header("Field of View Settings")]
        [Tooltip("Field of View when walking or standing still.")]
        [Range(40.0f, 120.0f)]
        [SerializeField] private float m_NormalFOV = 70.0f;

        [Tooltip("Field of View when sprinting at full speed.")]
        [Range(40.0f, 130.0f)]
        [SerializeField] private float m_SprintFOV = 82.0f;

        [Tooltip("Speed of interpolation between normal and sprint FOV.")]
        [SerializeField] private float m_FovTransitionSpeed = 8.0f;

        [Tooltip("Optional response curve mapping normalized sprint progress (0-1) to FOV interpolation factor.")]
        [SerializeField] private AnimationCurve m_FovResponseCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);

        [Header("Scalable VFX Hooks")]
        [Tooltip("Event invoked each frame when sprint intensity changes. Accepts a normalized float (0.0 = idle/walk, 1.0 = full sprint). Wire URP Volume weights, particle emission rates, or UI speed lines here.")]
        [SerializeField] private UnityEvent<float> m_OnSprintVFXIntensityChanged;

        [Tooltip("Event invoked when sprint transitions between active (true) and inactive (false).")]
        [SerializeField] private UnityEvent<bool> m_OnSprintStateToggled;

        // C# Events for high-performance code listeners
        public event Action<float> OnSprintVFXProgress;
        public event Action<bool> OnSprintVFXStateChanged;

        // Public Getters / Setters for runtime tuning
        public float NormalFOV { get => m_NormalFOV; set => m_NormalFOV = value; }
        public float SprintFOV { get => m_SprintFOV; set => m_SprintFOV = value; }
        public float FovTransitionSpeed { get => m_FovTransitionSpeed; set => m_FovTransitionSpeed = value; }
        public float CurrentFOV => m_CurrentFOV;
        public float CurrentVFXIntensity => m_CurrentVFXIntensity;

        // Internal state
        private float m_CurrentFOV;
        private float m_CurrentVFXIntensity;
        private ISprintVFXReceiver[] m_VfxReceivers;

        private void Awake()
        {
            if (m_MovementController == null)
            {
                m_MovementController = GetComponentInParent<FPSMovementController>();
                if (m_MovementController == null)
                {
                    m_MovementController = GetComponent<FPSMovementController>();
                }
            }

            if (m_CinemachineCamera == null)
            {
                m_CinemachineCamera = GetComponentInChildren<CinemachineCamera>();
                if (m_CinemachineCamera == null)
                {
                    m_CinemachineCamera = GetComponentInParent<CinemachineCamera>();
                }
            }

            m_CurrentFOV = m_NormalFOV;
            if (m_CinemachineCamera != null)
            {
                var lens = m_CinemachineCamera.Lens;
                lens.FieldOfView = m_NormalFOV;
                m_CinemachineCamera.Lens = lens;
            }

            RefreshVFXReceivers();
        }

        private void OnEnable()
        {
            if (m_MovementController != null)
            {
                m_MovementController.OnSprintStateChanged += HandleSprintStateChanged;
            }
        }

        private void OnDisable()
        {
            if (m_MovementController != null)
            {
                m_MovementController.OnSprintStateChanged -= HandleSprintStateChanged;
            }
        }

        /// <summary>
        /// Scans child/sibling objects for ISprintVFXReceiver implementations.
        /// Call whenever dynamic VFX components are added at runtime.
        /// </summary>
        public void RefreshVFXReceivers()
        {
            m_VfxReceivers = GetComponentsInChildren<ISprintVFXReceiver>(true);
        }

        private void HandleSprintStateChanged(bool isSprinting)
        {
            m_OnSprintStateToggled?.Invoke(isSprinting);
            OnSprintVFXStateChanged?.Invoke(isSprinting);
        }

        private void Update()
        {
            UpdateFOVAndVFX();
        }

        private void UpdateFOVAndVFX()
        {
            float targetProgress = 0.0f;
            if (m_MovementController != null)
            {
                targetProgress = m_MovementController.SprintProgress;
            }

            // Smoothly interpolate VFX intensity
            m_CurrentVFXIntensity = Mathf.MoveTowards(
                m_CurrentVFXIntensity,
                targetProgress,
                m_FovTransitionSpeed * Time.deltaTime
            );

            // Evaluate curve for non-linear zoom feel
            float curveFactor = m_FovResponseCurve != null 
                ? m_FovResponseCurve.Evaluate(m_CurrentVFXIntensity) 
                : m_CurrentVFXIntensity;

            // Target and apply FOV
            float targetFOV = Mathf.Lerp(m_NormalFOV, m_SprintFOV, curveFactor);
            m_CurrentFOV = Mathf.MoveTowards(m_CurrentFOV, targetFOV, m_FovTransitionSpeed * 10.0f * Time.deltaTime);

            if (m_CinemachineCamera != null)
            {
                var lens = m_CinemachineCamera.Lens;
                lens.FieldOfView = m_CurrentFOV;
                m_CinemachineCamera.Lens = lens;
            }

            // Dispatch to VFX receivers & events
            DispatchVFXIntensity(m_CurrentVFXIntensity);
        }

        private void DispatchVFXIntensity(float intensity)
        {
            m_OnSprintVFXIntensityChanged?.Invoke(intensity);
            OnSprintVFXProgress?.Invoke(intensity);

            if (m_VfxReceivers != null)
            {
                for (int i = 0; i < m_VfxReceivers.Length; i++)
                {
                    m_VfxReceivers[i]?.SetSprintVFXIntensity(intensity);
                }
            }
        }
    }
}
