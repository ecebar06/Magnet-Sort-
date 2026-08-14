using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WordButton : MonoBehaviour
{
    public string CategoryId { get; private set; }
    public bool IsMatched { get; private set; }

    private TableController controller;
    private Image background;
    private TextMeshProUGUI label;
    private Image iconImage;
    private Color defaultColor;

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        background = GetComponent<Image>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Initialize(TableController owner, string text, string categoryId, Color color, Sprite icon = null)
    {
        if (background == null || label == null)
            CacheReferences();

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
        if (!IsMatched)
            controller?.OnWordClicked(this);
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
}
