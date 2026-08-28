using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(Button), typeof(CanvasGroup))]
public class WordButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    private TableController controller;
    private Canvas rootCanvas;
    private RectTransform dragGhost;
    private Color defaultColor;
    private bool isDragging;
    private bool suppressNextClick;

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
        defaultColor = color;
        background.color = color;
        button.interactable = true;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        label.text = text;
        ShowIcon(icon);
    }

    public void ClearContent()
    {
        transform.DOKill();
        CategoryId = string.Empty;
        IsMatched = false;
        isDragging = false;
        suppressNextClick = false;
        if (label != null)
        {
            label.text = string.Empty;
            label.gameObject.SetActive(true);
        }
        if (iconImage != null)
        {
            iconImage.sprite = null;
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
    }

    private void ApplyContent(string text, string categoryId, Color color, Sprite icon)
    {
        CategoryId = categoryId;
        IsMatched = false;
        defaultColor = color;
        name = $"Word - {text}";
        label.text = text;
        background.color = color;
        button.interactable = true;
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
        SetSelected(false);
        dragGhost = controller.CreateWordGhost(this);
        canvasGroup.alpha = 0.25f;
        canvasGroup.blocksRaycasts = false;
        MoveGhostToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragGhost == null) return;
        MoveGhostToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        WordButton target = FindDropTarget(eventData);
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
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, eventData.position, eventCamera, out Vector3 worldPoint))
        {
            dragGhost.DOKill();
            dragGhost.DOMove(worldPoint, 0.06f).SetEase(Ease.OutQuad);
        }
    }

    private WordButton FindDropTarget(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out WordButton target) && target != this)
                return target;
        }

        return null;
    }

    public void SetSelected(bool selected)
    {
        if (!IsMatched)
            background.color = selected ? new Color(0.55f, 0.43f, 0.86f, 1f) : defaultColor;
    }

    public void SetMatched(bool matched)
    {
        IsMatched = matched;
        // The fridge artwork communicates a completed row through its holder and
        // category sticker, so keep the illustrated word magnets untinted.
        bool usesIllustratedMagnet = Mathf.Approximately(defaultColor.r, 1f) &&
                                     Mathf.Approximately(defaultColor.g, 1f) &&
                                     Mathf.Approximately(defaultColor.b, 1f);
        background.color = matched && !usesIllustratedMagnet
            ? new Color(0.55f, 0.88f, 0.68f, 1f)
            : defaultColor;
        if (button != null) button.interactable = !matched;
    }

    public void SetVisualAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    private bool HasRequiredReferences()
    {
        if (background != null && label != null && iconImage != null && button != null && canvasGroup != null)
            return true;

        Debug.LogError($"{name} has unassigned WordButton prefab references. Run Tools > Word Game > Setup Word Button Prefab.", this);
        return false;
    }
}
