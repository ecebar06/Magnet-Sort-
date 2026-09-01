using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(Button), typeof(CanvasGroup))]
public class WordButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string CategoryId { get; private set; }
    public bool IsMatched { get; private set; }
    public RowController Row { get; private set; }
    public RectTransform RectTransform => transform as RectTransform;

    [Header("Prefab References")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backlight;
    [SerializeField] private Image matchedOverlay;
    [SerializeField] private WordButtonStyleSettings styleSettings;

    private TableController controller;
    private Canvas rootCanvas;
    private RectTransform dragGhost;
    private Vector3 dragPointerOffset;
    private Vector3 pressRestPosition;
    private bool isPressed;
    private Color defaultColor;
    private bool tileVisualVisible = true;
    private Color matchedVisualColor;
    private bool hasMatchColor;
    private bool isDragging;
    private bool suppressNextClick;
    private WordButton highlightedDropTarget;
    private Selectable.Transition defaultButtonTransition = Selectable.Transition.ColorTint;

    private void Awake()
    {
        if (button != null) defaultButtonTransition = button.transition;
    }

    private void LateUpdate()
    {
        // Selectable/Layout rebuilds can run after gameplay code. Keep the
        // completed visual authoritative at the end of every rendered frame.
        if (hasMatchColor && tileVisualVisible) ApplyMatchedVisual();
    }

    private WordButtonStyleSettings Style
    {
        get
        {
            if (styleSettings == null) styleSettings = WordButtonStyleSettings.LoadDefault();
            return styleSettings;
        }
    }

    public void AssignRow(RowController row, TableController owner)
    {
        Row = row;
        controller = owner;
        rootCanvas = owner == null ? null : owner.GameplayCanvas;
    }

    public void Initialize(TableController owner, string text, string categoryId, Color color, Sprite icon = null)
    {
        if (!HasRequiredReferences()) return;

        controller = owner;
        rootCanvas = owner == null ? null : owner.GameplayCanvas;
        CategoryId = categoryId;
        IsMatched = false;
        hasMatchColor = false;
        tileVisualVisible = true;
        defaultColor = color;
        matchedVisualColor = Style == null ? new Color(0.05f, 0.82f, 0.43f, 1f) : Style.matchedWordAndRowColor;
        button.targetGraphic = background;
        background.color = color;
        background.CrossFadeColor(color, 0f, true, true);
        background.canvasRenderer.SetColor(color);
        button.transition = defaultButtonTransition;
        button.interactable = true;
        ColorBlock buttonColors = button.colors;
        buttonColors.disabledColor = Color.white;
        button.colors = buttonColors;
        canvasGroup.alpha = 1f;
        SetBacklight(false);
        if (matchedOverlay != null) matchedOverlay.gameObject.SetActive(false);
        gameObject.SetActive(true);
        SetContentVisible(true);
        SetVisualAlpha(1f);
        label.text = text;
        ShowIcon(icon);
        SetTileVisualVisible(true);
    }

    public void ClearContent()
    {
        transform.DOKill();
        CategoryId = string.Empty;
        IsMatched = false;
        hasMatchColor = false;
        isDragging = false;
        suppressNextClick = false;
        highlightedDropTarget = null;
        SetBacklight(false);
        if (matchedOverlay != null) matchedOverlay.gameObject.SetActive(false);
        if (label != null)
        {
            label.text = string.Empty;
            label.enabled = true;
            label.alpha = 1f;
            label.gameObject.SetActive(true);
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = true;
            Color iconColor = iconImage.color;
            iconColor.a = 1f;
            iconImage.color = iconColor;
            iconImage.gameObject.SetActive(false);
        }
        if (button != null) button.interactable = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    public void SwapContentWith(WordButton other)
    {
        if (other == null || other == this || !HasRequiredReferences() || !other.HasRequiredReferences()) return;

        string thisText = label.text;
        string thisCategory = CategoryId;
        Sprite thisIcon = iconImage.sprite;
        bool thisUsesIcon = iconImage.gameObject.activeSelf;
        Color thisColor = defaultColor;

        string otherText = other.label.text;
        string otherCategory = other.CategoryId;
        Sprite otherIcon = other.iconImage.sprite;
        bool otherUsesIcon = other.iconImage.gameObject.activeSelf;
        Color otherColor = other.defaultColor;

        ApplyContent(otherText, otherCategory, otherColor, otherUsesIcon ? otherIcon : null);
        other.ApplyContent(thisText, thisCategory, thisColor, thisUsesIcon ? thisIcon : null);
    }

    public void ConfigureAsGhost()
    {
        enabled = false;
        if (button != null) button.enabled = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        SetBacklight(true);
    }

    private void ApplyContent(string text, string categoryId, Color color, Sprite icon)
    {
        CategoryId = categoryId;
        IsMatched = false;
        hasMatchColor = false;
        defaultColor = color;
        matchedVisualColor = Style == null ? new Color(0.05f, 0.82f, 0.43f, 1f) : Style.matchedWordAndRowColor;
        name = $"Word - {text}";
        label.text = text;
        button.targetGraphic = background;
        background.color = color;
        background.CrossFadeColor(color, 0f, true, true);
        background.canvasRenderer.SetColor(color);
        button.transition = defaultButtonTransition;
        button.interactable = true;
        if (matchedOverlay != null) matchedOverlay.gameObject.SetActive(false);
        SetContentVisible(true);
        SetVisualAlpha(1f);
        ShowIcon(icon);
    }

    private void ShowIcon(Sprite icon)
    {
        bool hasIcon = icon != null;
        if (iconImage == null)
        {
            Debug.LogError($"{name} is missing its Icon Image reference. Run Tools > Word Game > Setup Word Button Prefab.", this);
            label.gameObject.SetActive(true);
            return;
        }

        iconImage.sprite = icon;
        iconImage.gameObject.SetActive(hasIcon);
        label.gameObject.SetActive(!hasIcon);
    }

    public void OnButtonClicked()
    {
        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if (!IsMatched && !isDragging && controller != null && !controller.IsInputLocked)
            controller.OnWordClicked(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsMatched || controller == null || controller.IsInputLocked) return;

        HapticFeedback.Play(HapticFeedback.Strength.Light);
        isPressed = true;
        pressRestPosition = transform.localPosition;
        SetBacklight(true);
        transform.DOKill();
        float duration = Style == null ? 0.12f : Mathf.Min(0.12f, Style.animationDuration);
        float pressScale = Style == null ? 1.15f : Style.dragScale;
        // Grow as soon as the pointer goes down, before the player has moved
        // far enough for Unity to start a drag.
        transform.DOScale(pressScale, duration).SetEase(Ease.OutBack);
        transform.DOLocalMoveY(pressRestPosition.y + 8f, duration).SetEase(Ease.OutCubic);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;
        isPressed = false;
        if (isDragging) return;

        SetBacklight(false);
        transform.DOKill();
        transform.DOScale(1f, 0.10f).SetEase(Ease.OutCubic);
        transform.DOLocalMove(pressRestPosition, 0.10f).SetEase(Ease.OutCubic);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsMatched || controller == null || controller.IsInputLocked)
        {
            eventData.pointerDrag = null;
            return;
        }

        if (rootCanvas == null || !controller.BeginWordDrag()) return;

        isDragging = true;
        suppressNextClick = true;
        SetBacklight(true);
        dragGhost = controller.CreateWordGhost(this);
        canvasGroup.blocksRaycasts = false;
        SetTileVisualVisible(false);

        if (dragGhost != null)
        {
            dragGhost.DOKill();
            float dragScale = Style == null ? 1.15f : Style.dragScale;
            float currentScale = Mathf.Max(0.01f, dragGhost.localScale.x);
            if (TryGetPointerWorldPosition(eventData, out Vector3 pointerWorld))
                dragPointerOffset = (dragGhost.position - pointerWorld) * (dragScale / currentScale);
            else
                dragPointerOffset = Vector3.zero;
            dragGhost.localScale = Vector3.one * dragScale;
            MoveGhostToPointer(eventData);
        }

        transform.DOKill();
        transform.localPosition = pressRestPosition;
        transform.localScale = Vector3.one;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragGhost == null) return;
        MoveGhostToPointer(eventData);
        UpdateDropTargetBacklight(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        isPressed = false;
        canvasGroup.blocksRaycasts = true;
        SetTileVisualVisible(true);
        SetBacklight(false);

        WordButton target = FindDropTarget(eventData);
        ClearDropTargetBacklight();
        Vector3 releasePosition = dragGhost == null ? transform.position : dragGhost.position;
        if (dragGhost != null)
        {
            dragGhost.DOKill();
            Destroy(dragGhost.gameObject);
        }
        dragGhost = null;

        if (target != null && target != this && !target.IsMatched)
            controller.TrySwapWords(this, target, releasePosition);
        else
            controller.AnimateRejectedDrop(this, releasePosition);
    }

    private void MoveGhostToPointer(PointerEventData eventData)
    {
        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPoint))
        {
            float lift = Style == null ? 10f : Style.dragLift;
            float scaleFactor = Mathf.Max(0.01f, rootCanvas.scaleFactor);
            worldPoint += dragPointerOffset + Vector3.up * (lift / scaleFactor);
            // Pointer-following must be immediate. A short DOMove tween looked
            // smooth in the Editor but made the card trail behind the finger
            // on an actual touch device.
            dragGhost.position = worldPoint;
        }
    }

    private bool TryGetPointerWorldPosition(PointerEventData eventData, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (rootCanvas == null) return false;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        return canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, eventData.position, eventCamera, out worldPoint);
    }

    private WordButton FindDropTarget(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (RaycastResult result in results)
        {
            WordButton target = result.gameObject.GetComponentInParent<WordButton>();
            if (target != null && target != this)
                return target;
        }

        return null;
    }

    public void SetSelected(bool selected)
    {
        if (IsMatched) return;
        SetBacklight(selected);
        float targetScale = selected && Style != null ? Style.selectedScale : 1f;
        float duration = Style == null ? 0.14f : Style.animationDuration;
        transform.DOKill();
        // OnPointerUp starts the return-to-slot tween, but the click callback
        // reaches here immediately afterwards. DOKill used to stop that move
        // while the button was still lifted, so repeated clicks accumulated
        // another lift each time. Selection only scales the card; keep it in
        // its recorded slot position.
        transform.localPosition = pressRestPosition;
        transform.DOScale(targetScale, duration)
            .SetEase(selected ? Ease.OutBack : Ease.OutCubic);
    }

    public void SetMatched(bool matched)
    {
        Color color = Style == null
            ? new Color(0.05f, 0.82f, 0.43f, 1f)
            : Style.matchedWordAndRowColor;
        SetMatched(matched, color);
    }

    public void SetMatched(bool matched, Color matchedColor)
    {
        SetMatchState(matched, matchedColor, matched);
    }

    public void SetPartialMatch(bool highlighted, Color color)
    {
        SetMatchState(highlighted, color, false);
    }

    private void SetMatchState(bool matched, Color matchedColor, bool locked)
    {
        IsMatched = locked;
        hasMatchColor = matched;
        if (matched) matchedVisualColor = matchedColor;
        Color targetColor = matched ? matchedVisualColor : defaultColor;
        targetColor.a = defaultColor.a;
        SetBacklight(false);
        if (button != null)
        {
            // A disabled ColorTint transition writes its own color to the
            // target Graphic. Disable the transition so it cannot overwrite
            // the completed-row green applied to the WordButton image.
            button.transition = matched ? Selectable.Transition.None : defaultButtonTransition;
            button.targetGraphic = matched ? null : background;
            button.interactable = !locked;
        }
        if (matched) ApplyMatchedVisual();
        else
        {
            background.color = targetColor;
            background.CrossFadeColor(targetColor, 0f, true, true);
            if (matchedOverlay != null) matchedOverlay.gameObject.SetActive(false);
        }
    }

    private void ApplyMatchedVisual()
    {
        Color matchedColor = matchedVisualColor;
        matchedColor.a = 1f;

        background.enabled = true;
        background.color = matchedColor;
        background.CrossFadeColor(matchedColor, 0f, true, true);

        if (matchedOverlay == null) return;
        matchedOverlay.enabled = true;
        matchedOverlay.color = matchedColor;
        matchedOverlay.rectTransform.anchorMin = Vector2.zero;
        matchedOverlay.rectTransform.anchorMax = Vector2.one;
        matchedOverlay.rectTransform.offsetMin = Vector2.zero;
        matchedOverlay.rectTransform.offsetMax = Vector2.zero;
        matchedOverlay.gameObject.SetActive(true);
        // Keep the overlay above the button art and below label/icon content.
        matchedOverlay.transform.SetAsLastSibling();
        if (label != null) label.transform.SetAsLastSibling();
        if (iconImage != null) iconImage.transform.SetAsLastSibling();
    }

    public void SetContentVisible(bool visible)
    {
        if (label != null) label.enabled = visible;
        if (iconImage != null) iconImage.enabled = visible;
    }

    public void SetTileVisualVisible(bool visible)
    {
        tileVisualVisible = visible;
        if (background != null) background.enabled = visible;
        if (matchedOverlay != null && hasMatchColor) matchedOverlay.enabled = visible;
        SetContentVisible(visible);
    }

    private void SetBacklight(bool visible)
    {
        if (backlight != null && Style != null)
        {
            Color lightColor = Style.backlightColor;
            // The task allows an empty placeholder until the light texture is
            // supplied. A sprite-less UI Image would otherwise draw a solid box.
            if (backlight.sprite == null) lightColor.a = 0f;
            backlight.color = lightColor;
        }
        if (backlight != null) backlight.gameObject.SetActive(visible);
    }

    private void UpdateDropTargetBacklight(PointerEventData eventData)
    {
        WordButton target = FindDropTarget(eventData);
        if (target == this || (target != null && target.IsMatched)) target = null;
        if (target == highlightedDropTarget) return;
        ClearDropTargetBacklight();
        highlightedDropTarget = target;
        if (highlightedDropTarget != null)
        {
            highlightedDropTarget.SetDropTargetHighlighted(true);
            HapticFeedback.Play(HapticFeedback.Strength.Light);
        }
    }

    private void ClearDropTargetBacklight()
    {
        if (highlightedDropTarget != null) highlightedDropTarget.SetDropTargetHighlighted(false);
        highlightedDropTarget = null;
    }

    private void SetDropTargetHighlighted(bool highlighted)
    {
        if (IsMatched) return;

        SetBacklight(highlighted);
        float scale = highlighted
            ? (Style == null ? 1.08f : Style.dropTargetScale)
            : 1f;
        float duration = Style == null ? 0.12f : Style.animationDuration;
        transform.DOKill();
        transform.DOScale(scale, duration).SetEase(highlighted ? Ease.OutBack : Ease.OutCubic);
    }

    public void SetVisualAlpha(float alpha)
    {
        // Keep the authored magnet/background fully opaque. Only its content
        // disappears while the animated drag/swap copy is moving.
        if (label != null) label.alpha = alpha;
        if (iconImage != null)
        {
            Color iconColor = iconImage.color;
            iconColor.a = alpha;
            iconImage.color = iconColor;
        }
    }

    private bool HasRequiredReferences()
    {
        if (background != null && label != null && iconImage != null && button != null && canvasGroup != null &&
            backlight != null && matchedOverlay != null)
            return true;

        Debug.LogError($"{name} has unassigned WordButton prefab references. Run Tools > Word Game > Setup Word Button Prefab.", this);
        return false;
    }
}
