using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WordButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string CategoryId { get; private set; }
    public bool IsMatched { get; private set; }

    private TableController controller;
    private Image background;
    private TextMeshProUGUI label;
    private Image iconImage;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform dragGhost;
    private Color defaultColor;
    private bool isDragging;
    private bool suppressNextClick;

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        background = GetComponent<Image>();
        label = GetComponentInChildren<TextMeshProUGUI>();
        rootCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(TableController owner, string text, string categoryId, Color color, Sprite icon = null)
    {
        if (background == null || label == null) CacheReferences();
        controller = owner;
        CategoryId = categoryId;
        defaultColor = color;
        background.color = color;
        label.text = text;
        ShowIcon(icon);
    }

    private void ShowIcon(Sprite icon)
    {
        if (iconImage == null)
        {
            GameObject iconObject = new GameObject("WordIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(transform, false);
            iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            RectTransform rect = iconImage.rectTransform;
            rect.anchorMin = new Vector2(0.20f, 0.12f);
            rect.anchorMax = new Vector2(0.80f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        bool hasIcon = icon != null;
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

        rootCanvas = GetComponentInParent<Canvas>();
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
            WordButton target = result.gameObject.GetComponentInParent<WordButton>();
            if (target != null && target != this) return target;
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
        background.color = matched ? new Color(0.55f, 0.88f, 0.68f, 1f) : defaultColor;
        Button button = GetComponent<Button>();
        if (button != null) button.interactable = !matched;
    }

    public void SetVisualAlpha(float alpha)
    {
        if (canvasGroup == null) CacheReferences();
        canvasGroup.alpha = alpha;
    }
}
