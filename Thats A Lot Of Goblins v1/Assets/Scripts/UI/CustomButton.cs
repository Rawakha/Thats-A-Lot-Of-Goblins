using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CustomButton : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] public UnityEvent OnClicked;
    [SerializeField] public UnityEvent OnPointerEntered;
    [SerializeField] public UnityEvent OnPointerExited;

    [Header("Animation")]
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float hoverRotation = 3f;
    [SerializeField] private float hoverDuration = 0.2f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;

    [Header("Click Spring")]
    [SerializeField] private float springStiffness = 400f;
    [SerializeField] private float springDamping = 18f;
    [SerializeField] private float clickImpulse = 6f;

    [Header("Idle")]
    [SerializeField] private bool useIdleAnimation = false;
    [SerializeField] private float idleScaleAmount = 0f;
    [SerializeField] private float idleRotationAmount = 1f;
    [SerializeField] private float idleSpeed = 2f;

    [Header("Hover Idle")]
    [SerializeField] private bool useHoverIdleAnimation = false;
    [SerializeField] private float hoverIdleScaleAmount = 0.04f;
    [SerializeField] private float hoverIdleRotationAmount = 2f;
    [SerializeField] private float hoverIdleSpeed = 5f;

    private RectTransform rect;
    private float baseScale = 1f;
    private float baseRotation = 0f;
    private float springValue = 1f;
    private float springVelocity;
    private float idlePhase;
    private bool isHovered;

    private Tween scaleTween;
    private Tween rotationTween;

    public RectTransform Rect => rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        baseScale = rect.localScale.x;
        baseRotation = rect.localEulerAngles.z;

        idlePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        hoverRotation *= UnityEngine.Random.value > 0.5f ? 1f : -1f;
    }

    private void Update()
    {
        float speed = isHovered ? hoverIdleSpeed : idleSpeed;
        float rotationAmount = isHovered ? hoverIdleRotationAmount : idleRotationAmount;
        float scaleAmount = isHovered ? hoverIdleScaleAmount : idleScaleAmount;

        idlePhase += speed * Time.unscaledDeltaTime;

        float idleScale = (float)(1f + Math.Sin(idlePhase * 0.7f) * scaleAmount);
        float idleRotation = (float)(Math.Sin(idlePhase) * rotationAmount);

        // Click Spring
        float force = (1f - springValue) * springStiffness - springVelocity * springDamping;
        springVelocity += force * Time.unscaledDeltaTime;
        springValue += springVelocity * Time.unscaledDeltaTime;
        springValue = Mathf.Max(springValue, 0.1f);

        // Compose
        float composedScale = baseScale * idleScale * springValue;
        rect.localScale = Vector3.one * composedScale;
        rect.localRotation = Quaternion.Euler(0f, 0f, baseRotation + idleRotation);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        OnClicked?.Invoke();
        springVelocity -= clickImpulse;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        OnPointerEntered?.Invoke();

        TweenTo(hoverScale, hoverRotation, hoverDuration, hoverEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        OnPointerExited?.Invoke();

        TweenTo(baseScale, baseRotation, hoverDuration, hoverEase);
    }

    private void TweenTo(float scale, float rotation, float duration, Ease ease)
    {
        scaleTween?.Kill();
        rotationTween?.Kill();

        scaleTween = DOTween.To(() => baseScale, v => baseScale = v, scale, duration).SetEase(ease);
        rotationTween = DOTween.To(() => baseRotation, v => baseRotation = v, rotation, duration).SetEase(ease);
    }
}