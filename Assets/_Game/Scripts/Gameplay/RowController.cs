using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the authored parts of one board row. The four word slots are part of
/// RowPrefab; gameplay only changes their content.
/// </summary>
public class RowController : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private Image holderImage;
    [SerializeField] private WordButton[] wordSlots = new WordButton[4];
    [Header("Row Style")]
    [SerializeField] private Color normalHolderColor = Color.white;
    [SerializeField] private Color completedHolderColor = new Color(0.72f, 0.76f, 0.74f, 1f);
    [Header("Completed Category Label")]
    [SerializeField] private Image categoryCard;
    [SerializeField] private Image categoryOrange;
    [SerializeField] private TextMeshProUGUI categoryLabel;
    [SerializeField] private CanvasGroup categoryCanvasGroup;

    public IReadOnlyList<WordButton> WordSlots => wordSlots;
    public int ActiveWordCount
    {
        get
        {
            int count = 0;
            if (wordSlots == null) return count;
            foreach (WordButton slot in wordSlots)
                if (slot != null && slot.gameObject.activeSelf) count++;
            return count;
        }
    }

    public bool HasFourSlots()
    {
        if (wordSlots == null || wordSlots.Length != 4) return false;
        for (int i = 0; i < wordSlots.Length; i++)
            if (wordSlots[i] == null) return false;
        return true;
    }

    public void PrepareForLevel(TableController owner)
    {
        if (!HasFourSlots()) return;

        if (holderImage != null) holderImage.color = normalHolderColor;
        HideCompletedCategoryLabel();
        foreach (WordButton slot in wordSlots)
        {
            slot.AssignRow(this, owner);
            slot.ClearContent();
        }
    }

    public WordButton SetWord(int slotIndex, TableController owner, string text,
        string categoryId, Color color, Sprite icon = null)
    {
        if (!HasFourSlots() || slotIndex < 0 || slotIndex >= wordSlots.Length) return null;

        WordButton slot = wordSlots[slotIndex];
        slot.AssignRow(this, owner);
        slot.name = $"Word - {text}";
        slot.Initialize(owner, text, categoryId, color, icon);
        return slot;
    }

    public int GetFirstEmptySlotIndex()
    {
        if (!HasFourSlots()) return -1;
        for (int index = 0; index < wordSlots.Length; index++)
            if (!wordSlots[index].gameObject.activeSelf) return index;
        return -1;
    }

    public WordButton[] GetActiveWords()
    {
        if (!HasFourSlots()) return System.Array.Empty<WordButton>();

        List<WordButton> activeWords = new List<WordButton>(4);
        foreach (WordButton slot in wordSlots)
            if (slot.gameObject.activeSelf) activeWords.Add(slot);
        return activeWords.ToArray();
    }

    public void ClearWords()
    {
        if (!HasFourSlots()) return;
        foreach (WordButton slot in wordSlots) slot.ClearContent();
        if (holderImage != null) holderImage.color = normalHolderColor;
        HideCompletedCategoryLabel();
    }

    public void CompleteCategory(string categoryName)
    {
        if (!HasFourSlots()) return;
        foreach (WordButton slot in wordSlots)
            if (slot.gameObject.activeSelf) slot.SetMatched(true);
        if (holderImage != null) holderImage.color = completedHolderColor;
        ShowCompletedCategoryLabel(categoryName);
    }

    public void HideCompletedCategoryLabel()
    {
        if (categoryCard == null) return;
        categoryCard.gameObject.SetActive(false);
        categoryCard.rectTransform.localScale = Vector3.one;
        if (categoryCanvasGroup != null) categoryCanvasGroup.alpha = 1f;
    }

    public void ShowCompletedCategoryLabel(string categoryName)
    {
        if (categoryCard == null || categoryLabel == null || categoryCanvasGroup == null) return;

        categoryLabel.text = string.IsNullOrWhiteSpace(categoryName) ? "category" : categoryName.ToLowerInvariant();
        categoryLabel.fontSize = 19f;
        categoryLabel.enableAutoSizing = false;
        categoryLabel.fontStyle = FontStyles.Normal;
        categoryLabel.alignment = TextAlignmentOptions.Center;
        categoryLabel.margin = Vector4.zero;

        RectTransform cardRect = categoryCard.rectTransform;
        RectTransform labelRect = categoryLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        // The orange overlaps the left side of the yellow card. Reserve its
        // space explicitly so the text always starts to the orange's right.
        labelRect.offsetMin = new Vector2(60f, 5f);
        labelRect.offsetMax = new Vector2(-28f, -5f);

        categoryLabel.ForceMeshUpdate();
        float textWidth = categoryLabel.preferredWidth;
        // The card follows the actual text width while keeping the font size
        // fixed and preserving enough room for the overlapping orange.
        float cardWidth = Mathf.Clamp(textWidth + 88f, 116f, 360f);
        cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);

        categoryCard.gameObject.SetActive(true);
        categoryCanvasGroup.DOKill();
        cardRect.DOKill();
        categoryCanvasGroup.alpha = 0f;
        cardRect.localScale = Vector3.one * 0.76f;
        categoryCanvasGroup.DOFade(1f, 0.18f);
        cardRect.DOScale(1f, 0.24f).SetEase(Ease.OutBack);
    }
}
