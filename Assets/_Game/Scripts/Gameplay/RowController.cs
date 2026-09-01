using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private WordButtonStyleSettings styleSettings;
    private Color rowMatchColor;
    private Color secondaryMatchColor;
    private readonly Dictionary<string, Color> partialMatchColors = new Dictionary<string, Color>();
    public bool IsCompleted { get; private set; }
    public WordButton LastArrivedWord { get; set; }
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

    public void PrepareForLevel(TableController owner, Color matchColor, Color secondaryColor)
    {
        if (!HasFourSlots()) return;

        rowMatchColor = matchColor;
        rowMatchColor.a = 1f;
        secondaryMatchColor = secondaryColor;
        secondaryMatchColor.a = 1f;
        IsCompleted = false;
        LastArrivedWord = null;
        partialMatchColors.Clear();
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
        partialMatchColors.Clear();
        if (holderImage != null) holderImage.color = normalHolderColor;
        IsCompleted = false;
        HideCompletedCategoryLabel();
    }

    public void CompleteCategory(string categoryName)
    {
        if (!HasFourSlots()) return;
        StartCoroutine(AnimateCompletedStyle(categoryName, true));
    }

    public void ApplyCompletedStyle()
    {
        if (!HasFourSlots()) return;
        LockCompletedWords();
        if (holderImage != null)
            holderImage.color = rowMatchColor;
    }

    public IEnumerator AnimateCompletedStyle(string categoryName = null, bool showCategoryLabel = false,
        bool colorHolder = true)
    {
        if (!HasFourSlots()) yield break;

        LockCompletedWords();

        Sequence cascade = DOTween.Sequence();
        for (int index = 0; index < wordSlots.Length; index++)
        {
            WordButton slot = wordSlots[index];
            if (slot == null || !slot.gameObject.activeSelf) continue;

            slot.transform.DOKill();
            slot.transform.localScale = Vector3.one;
            Tween pulse = slot.transform.DOScale(1.12f, 0.13f)
                .SetEase(Ease.OutCubic)
                .SetLoops(2, LoopType.Yoyo);
            cascade.Insert(index * 0.08f, pulse);
        }

        yield return cascade.WaitForCompletion();

        if (colorHolder && holderImage != null)
        {
            holderImage.DOKill();
            yield return holderImage.DOColor(rowMatchColor, 0.20f)
                .SetEase(Ease.OutCubic)
                .WaitForCompletion();
        }

        if (showCategoryLabel)
            ShowCompletedCategoryLabel(categoryName);
    }

    private void LockCompletedWords()
    {
        IsCompleted = true;
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        foreach (WordButton slot in wordSlots)
            if (slot.gameObject.activeSelf) slot.SetMatched(true, rowMatchColor);
    }

    public void ApplyMatchingWordStyle()
    {
        if (!HasFourSlots() || IsCompleted) return;

        WordButton[] words = GetActiveWords();
        var groups = words.Where(word => !string.IsNullOrWhiteSpace(word.CategoryId))
            .GroupBy(word => word.CategoryId).Where(group => group.Count() >= 2).ToArray();
        // Keep a group's assigned color while it remains paired, even if slots swap.
        foreach (string category in partialMatchColors.Keys.ToArray())
            if (!groups.Any(group => group.Key == category)) partialMatchColors.Remove(category);

        foreach (WordButton word in words) word.SetPartialMatch(false, Color.white);
        foreach (var group in groups)
        {
            if (!partialMatchColors.TryGetValue(group.Key, out Color color))
            {
                color = FindAvailableMatchColor();
                partialMatchColors.Add(group.Key, color);
            }
            foreach (WordButton word in group)
                word.SetPartialMatch(true, color);
        }
    }

    private Color FindAvailableMatchColor()
    {
        if (!partialMatchColors.Values.Contains(rowMatchColor)) return rowMatchColor;
        return secondaryMatchColor;
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
        categoryLabel.fontSize = 26f;
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
        // Give very short names (pets, days, etc.) a less cramped yellow
        // sticker without changing its sliced Image or longer-name sizing.
        float cardWidth = Mathf.Clamp(textWidth + 88f, 150f, 360f);
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
