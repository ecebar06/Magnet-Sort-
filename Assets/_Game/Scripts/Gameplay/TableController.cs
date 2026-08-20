using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TableController : MonoBehaviour
{
    [Header("Board Data")]
    public TextAsset jsonFile;
    public GameObject buttonPrefab;
    public GameObject rowPrefab;
    public Transform wordContainer;

    [Header("Level")]
    [Min(0)] public int startingLevelIndex = 0;

    [HideInInspector] public int currentLevelRows;

    private static TableController instance;
    [Header("Scene UI")]
    [SerializeField] private RectTransform gameHud;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform progressKnob;
    [SerializeField] private TextMeshProUGUI progressKnobText;
    [SerializeField] private TextMeshProUGUI moveCountText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI hintCountText;
    [SerializeField] private Transform progressDotsParent;
    [Header("Result Screens")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private TextMeshProUGUI winGoldText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button doubleRewardButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button watchAdContinueButton;
    [SerializeField] private Button spendGoldContinueButton;
    [SerializeField] private Button viewBoardButton;
    [SerializeField] private TextMeshProUGUI loseExplanationText;

    private readonly List<Image> progressDots = new List<Image>();
    private readonly Dictionary<string, string> categoryNames = new Dictionary<string, string>();
    private readonly Dictionary<string, Category> categoryDefinitions = new Dictionary<string, Category>();
    private readonly Queue<BoardWord> pendingWords = new Queue<BoardWord>();
    private WordButton selectedWord;
    private int remainingMoves;
    private bool gameEnded;
    private int categoriesCompleted;
    private int moveCount;
    private bool inputLocked;
    private bool rewardDoubled;
    private int paidReviveCount;
    private bool transformationInProgress;
    private static Sprite roundedPanelSprite;
    private static Sprite roundedButtonSprite;
    private static Sprite bulbIconSprite;
    private static Sprite gearIconSprite;
    private static Sprite lockIconSprite;
    private static readonly Color PastelButtonColor = new Color(1.00f, 0.96f, 0.84f, 1f);
    private WordIconLibrary iconLibrary;
    private TMP_FontAsset gameFont;
    private LevelDatabase levelDatabase;
    private int currentLevelNumber = 1;
    private int totalCategoryCount;
    private int activeLevelIndex;
    private PlayerSaveData playerSaveData;

    public bool IsInputLocked => inputLocked || gameEnded || transformationInProgress;

    private class BoardWord
    {
        public WordItem word;
        public string categoryId;
    }

    private void Awake()
    {
        // Prevent the duplicate scene component from creating a second board.
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        if (instance != this) return;
        // The editor hides the generated board while the game is stopped.
        // Always restore it when gameplay starts.
        if (wordContainer != null) wordContainer.gameObject.SetActive(true);
        playerSaveData = SaveSystem.Load();
        CreateScreenChrome();
        LoadLevel(Mathf.Max(startingLevelIndex, playerSaveData.currentLevelIndex));
    }

    public void LoadLevel(int levelIndex)
    {
        if (buttonPrefab == null || rowPrefab == null || wordContainer == null)
        {
            Debug.LogError("TableController references are not fully assigned.", this);
            return;
        }

        levelDatabase = LevelDatabase.LoadFromResources();
        iconLibrary = Resources.Load<WordIconLibrary>("Data/MainWordIconLibrary");
        if (gameFont == null)
            gameFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        activeLevelIndex = levelDatabase == null || levelDatabase.levels.Count == 0
            ? 0
            : Mathf.Clamp(levelIndex, 0, levelDatabase.levels.Count - 1);
        LevelData level = levelDatabase == null ? null : levelDatabase.GetLevel(activeLevelIndex);
        if (level == null)
        {
            Debug.LogError("No JSON levels were found in Resources/Data/Levels. Use the Level Editor or import a web level JSON.", this);
            return;
        }

        if (!level.IsValid(out string validationError))
        {
            Debug.LogError($"Level {level.levelNumber} is invalid: {validationError}", level);
            return;
        }

        totalCategoryCount = level.categories.Count;
        currentLevelNumber = level.levelNumber;
        categoriesCompleted = 0;
        remainingMoves = level.moveCount;
        moveCount = remainingMoves;
        gameEnded = false;
        inputLocked = false;
        transformationInProgress = false;
        rewardDoubled = false;
        paidReviveCount = 0;
        selectedWord = null;
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        if (loseExplanationText != null) loseExplanationText.text = "YOU RAN OUT OF MOVES";
        Canvas canvas = wordContainer.GetComponentInParent<Canvas>();
        Transform previousResult = canvas == null ? null : canvas.transform.Find("ResultOverlay");
        if (previousResult != null) Destroy(previousResult.gameObject);
        ClearBoard();
        ApplyBoardStyle();

        List<Category> selectedCategories = level.categories;
        categoryNames.Clear();
        categoryDefinitions.Clear();
        foreach (Category category in selectedCategories)
        {
            if (category == null || string.IsNullOrWhiteSpace(category.id)) continue;
            categoryNames[category.id] = string.IsNullOrWhiteSpace(category.name) ? category.id : category.name;
            categoryDefinitions[category.id] = category;
        }

        HashSet<string> generatedWords = new HashSet<string>(
            selectedCategories.Where(category => category.transformsOnComplete && category.transformResult != null)
                .Select(category => category.transformResult.text), System.StringComparer.OrdinalIgnoreCase);

        List<BoardWord> allPlayableWords = level.orderedWords != null && level.orderedWords.Count > 0
            ? level.orderedWords.Where(entry => entry != null && entry.word != null).Select(entry => new BoardWord
            {
                word = entry.word,
                categoryId = entry.categoryId
            }).ToList()
            : Enumerable.Range(0, 4).SelectMany(wordIndex => selectedCategories
                    .Where(category => category.words != null && category.words.Count > wordIndex)
                    .Select(category => new { category, word = category.words[wordIndex] })
                    .Where(item => !generatedWords.Contains(item.word.text))
                    .Select(item => new BoardWord { word = item.word, categoryId = item.category.id }))
                .ToList();

        int requestedRows = level.visibleRowCount > 0 ? level.visibleRowCount : selectedCategories.Count;
        currentLevelRows = Mathf.Clamp(requestedRows, 1, Mathf.Max(1, allPlayableWords.Count / 4));
        int visibleWordCount = currentLevelRows * 4;

        List<BoardWord> shuffledWords = allPlayableWords.Take(visibleWordCount).ToList();

        pendingWords.Clear();
        foreach (BoardWord queuedWord in allPlayableWords.Skip(visibleWordCount))
            pendingWords.Enqueue(queuedWord);

        const int columns = 4;
        for (int rowIndex = 0; rowIndex < currentLevelRows; rowIndex++)
        {
            GameObject newRow = Instantiate(rowPrefab, wordContainer);
            newRow.name = $"Row {rowIndex + 1}";
            Image rowBackground = newRow.GetComponent<Image>();
            if (rowBackground != null)
            {
                Color[] rowColors =
                {
                    new Color(0.20f, 0.65f, 0.98f, 1f),
                    new Color(0.52f, 0.34f, 0.88f, 1f),
                    new Color(1.00f, 0.69f, 0.12f, 1f),
                    new Color(0.22f, 0.82f, 0.67f, 1f),
                    new Color(0.98f, 0.42f, 0.58f, 1f)
                };
                rowBackground.sprite = GetRoundedPanelSprite();
                rowBackground.type = Image.Type.Sliced;
                rowBackground.color = rowColors[rowIndex % rowColors.Length];
                AddShadow(newRow, new Color(0.12f, 0.08f, 0.30f, 0.34f), new Vector2(0f, -7f));
                Outline rowOutline = newRow.GetComponent<Outline>();
                if (rowOutline == null) rowOutline = newRow.AddComponent<Outline>();
                rowOutline.effectColor = new Color(0.08f, 0.10f, 0.28f, 0.82f);
                rowOutline.effectDistance = new Vector2(3f, -3f);
                rowOutline.useGraphicAlpha = true;
            }

            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                BoardWord boardWord = shuffledWords[rowIndex * columns + columnIndex];
                WordItem word = boardWord.word;
                GameObject newButton = Instantiate(buttonPrefab, newRow.transform);
                newButton.name = $"Word - {word.text}";

                Image buttonImage = newButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.sprite = GetRoundedButtonSprite();
                    buttonImage.type = Image.Type.Sliced;
                    buttonImage.color = PastelButtonColor;
                    AddShadow(newButton, new Color(0.08f, 0.08f, 0.22f, 0.48f), new Vector2(0f, -7f));
                    Outline outline = newButton.GetComponent<Outline>();
                    if (outline == null) outline = newButton.AddComponent<Outline>();
                    outline.effectColor = new Color(0.08f, 0.10f, 0.24f, 0.82f);
                    outline.effectDistance = new Vector2(2f, -2f);
                    outline.useGraphicAlpha = true;
                }

                TextMeshProUGUI label = newButton.GetComponentInChildren<TextMeshProUGUI>();
                label.text = word.text;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22;
                label.fontSizeMax = 32;
                label.alignment = TextAlignmentOptions.Center;
                label.margin = new Vector4(8f, 4f, 8f, 4f);
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.16f, 0.12f, 0.28f, 1f);
                if (gameFont != null) label.font = gameFont;
                label.outlineColor = new Color(0.06f, 0.08f, 0.20f, 0.55f);
                label.outlineWidth = 0.08f;

                Sprite wordIcon = null;
                if (word.hasSprite && iconLibrary != null)
                    iconLibrary.TryGetIcon(word.spriteKey, out wordIcon);

                if (word.hasSprite && wordIcon == null)
                    Debug.LogWarning($"Icon not found for key '{word.spriteKey}'. Falling back to text.", this);

                WordButton wordButton = newButton.GetComponent<WordButton>();
                wordButton.Initialize(this, word.text, boardWord.categoryId, PastelButtonColor, wordIcon);
            }
        }

        ResizeBoard(currentLevelRows);
        RefreshIndicators();
    }

    public void OnWordClicked(WordButton word)
    {
        if (gameEnded || word == null || word.IsMatched) return;

        if (selectedWord == null)
        {
            selectedWord = word;
            selectedWord.SetSelected(true);
            return;
        }

        if (selectedWord == word)
        {
            selectedWord.SetSelected(false);
            selectedWord = null;
            return;
        }

        WordButton first = selectedWord;
        selectedWord.SetSelected(false);
        selectedWord = null;
        TrySwapWords(first, word);
    }

    public bool BeginWordDrag()
    {
        if (IsInputLocked) return false;
        inputLocked = true;
        return true;
    }

    public RectTransform CreateWordGhost(WordButton source)
    {
        if (source == null) return null;
        Canvas canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        GameObject ghost = Instantiate(source.gameObject, canvas.transform, true);
        ghost.name = $"{source.name} Ghost";
        WordButton ghostWord = ghost.GetComponent<WordButton>();
        if (ghostWord != null) ghostWord.enabled = false;
        Button ghostButton = ghost.GetComponent<Button>();
        if (ghostButton != null) ghostButton.enabled = false;
        CanvasGroup group = ghost.GetComponent<CanvasGroup>();
        if (group == null) group = ghost.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
        ghost.transform.SetAsLastSibling();
        ghost.transform.DOKill();
        ghost.transform.DOScale(ghost.transform.localScale * 1.06f, 0.12f).SetEase(Ease.OutCubic);
        return ghost.GetComponent<RectTransform>();
    }

    public void TrySwapWords(WordButton first, WordButton second, Vector3? firstVisualStart = null)
    {
        if (first == null || second == null || first == second || first.IsMatched || second.IsMatched)
        {
            inputLocked = false;
            return;
        }

        inputLocked = true;
        Transform firstRow = first.transform.parent;
        Transform secondRow = second.transform.parent;
        int firstIndex = first.transform.GetSiblingIndex();
        int secondIndex = second.transform.GetSiblingIndex();
        bool changedRows = firstRow != secondRow;

        Vector3 firstStart = firstVisualStart ?? first.transform.position;
        Vector3 secondStart = second.transform.position;

        if (changedRows)
        {
            first.transform.SetParent(secondRow, false);
            second.transform.SetParent(firstRow, false);
            first.transform.SetSiblingIndex(secondIndex);
            second.transform.SetSiblingIndex(firstIndex);
        }
        else
        {
            first.transform.SetSiblingIndex(secondIndex);
            second.transform.SetSiblingIndex(firstIndex);
        }

        Canvas.ForceUpdateCanvases();
        Vector3 firstTarget = first.transform.position;
        Vector3 secondTarget = second.transform.position;
        RectTransform firstGhost = CreateWordGhost(first);
        RectTransform secondGhost = CreateWordGhost(second);
        if (firstGhost == null || secondGhost == null)
        {
            if (firstGhost != null) Destroy(firstGhost.gameObject);
            if (secondGhost != null) Destroy(secondGhost.gameObject);
            FinishSwap(first, second, firstRow, secondRow, changedRows);
            return;
        }

        firstGhost.position = firstStart;
        secondGhost.position = secondStart;
        first.SetVisualAlpha(0f);
        second.SetVisualAlpha(0f);

        Sequence swapSequence = DOTween.Sequence();
        swapSequence.Join(firstGhost.DOMove(firstTarget, 0.28f).SetEase(Ease.OutCubic));
        swapSequence.Join(secondGhost.DOMove(secondTarget, 0.28f).SetEase(Ease.OutCubic));
        swapSequence.OnComplete(() =>
        {
            first.SetVisualAlpha(1f);
            second.SetVisualAlpha(1f);
            Destroy(firstGhost.gameObject);
            Destroy(secondGhost.gameObject);
            FinishSwap(first, second, firstRow, secondRow, changedRows);
        });
    }

    public void AnimateRejectedDrop(WordButton source, Vector3 releasePosition)
    {
        if (source == null)
        {
            inputLocked = false;
            return;
        }

        inputLocked = true;
        RectTransform ghost = CreateWordGhost(source);
        if (ghost == null)
        {
            inputLocked = false;
            return;
        }

        ghost.position = releasePosition;
        source.SetVisualAlpha(0f);
        ghost.DOMove(source.transform.position, 0.22f).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            source.SetVisualAlpha(1f);
            Destroy(ghost.gameObject);
            inputLocked = false;
        });
    }

    private void FinishSwap(WordButton first, WordButton second, Transform firstRow, Transform secondRow, bool changedRows)
    {
        if (changedRows)
        {
            remainingMoves = Mathf.Max(0, remainingMoves - 1);
            moveCount = remainingMoves;
        }

        CheckRow(firstRow);
        if (secondRow != firstRow) CheckRow(secondRow);
        RefreshIndicators();
        inputLocked = false;

        if (categoriesCompleted >= totalCategoryCount && !transformationInProgress)
            EndGame(true);
        else if (remainingMoves <= 0 && !transformationInProgress)
            EndGame(false);
    }

    private void CheckRow(Transform row)
    {
        if (transformationInProgress) return;
        WordButton[] words = row.GetComponentsInChildren<WordButton>();
        if (words.Length != 4 || words.Any(word => word.IsMatched)) return;

        string categoryId = words[0].CategoryId;
        if (words.All(word => word.CategoryId == categoryId))
        {
            bool transforms = categoryDefinitions.TryGetValue(categoryId, out Category category) &&
                              category.transformsOnComplete;

            categoriesCompleted++;
            if (transforms)
                TransformCompletedCategory(row, words, category);
            else
            {
                foreach (WordButton word in words) word.SetMatched(true);
                ShowCompletedCategoryName(row, categoryId);
            }
        }
    }

    private void TransformCompletedCategory(Transform row, WordButton[] words, Category category)
    {
        if (category.transformResult == null) return;
        StartCoroutine(AnimateCategoryTransformation(row, words, category));
    }

    private IEnumerator AnimateCategoryTransformation(Transform row, WordButton[] words, Category category)
    {
        transformationInProgress = true;
        inputLocked = true;

        Vector3 mergePoint = Vector3.zero;
        foreach (WordButton word in words) mergePoint += word.transform.position;
        mergePoint /= words.Length;

        List<RectTransform> mergeGhosts = new List<RectTransform>();
        Sequence mergeSequence = DOTween.Sequence();
        foreach (WordButton word in words)
        {
            RectTransform ghost = CreateWordGhost(word);
            if (ghost != null)
            {
                ghost.DOKill();
                mergeGhosts.Add(ghost);
                mergeSequence.Join(ghost.DOMove(mergePoint, 0.38f).SetEase(Ease.InCubic));
                mergeSequence.Join(ghost.DOScale(0.18f, 0.38f).SetEase(Ease.InBack));
                mergeSequence.Join(ghost.DORotate(new Vector3(0f, 0f, Random.Range(-12f, 12f)), 0.38f));
            }
            word.SetVisualAlpha(0f);
            word.SetMatched(true);
        }

        yield return mergeSequence.WaitForCompletion();
        foreach (RectTransform ghost in mergeGhosts)
            if (ghost != null) Destroy(ghost.gameObject);
        foreach (WordButton word in words)
        {
            word.gameObject.SetActive(false);
            Destroy(word.gameObject);
        }

        BoardWord result = new BoardWord
        {
            word = category.transformResult,
            categoryId = category.transformResultCategoryId
        };
        GameObject transformed = CreateRuntimeWordButton(result, row);
        transformed.transform.localScale = Vector3.zero;

        List<GameObject> droppedWords = new List<GameObject>();
        int openSlots = Mathf.Max(0, 4 - row.GetComponentsInChildren<WordButton>().Length);
        for (int slot = 0; slot < openSlots && pendingWords.Count > 0; slot++)
        {
            BoardWord nextWord = pendingWords.Dequeue();
            GameObject droppedWord = CreateRuntimeWordButton(nextWord, row);
            droppedWord.transform.localScale = Vector3.zero;
            droppedWords.Add(droppedWord);
        }

        Canvas.ForceUpdateCanvases();
        yield return transformed.transform.DOScale(1f, 0.34f).SetEase(Ease.OutBack).WaitForCompletion();
        for (int i = 0; i < droppedWords.Count; i++)
        {
            droppedWords[i].transform.DOScale(1f, 0.27f).SetDelay(i * 0.07f).SetEase(Ease.OutBack);
        }
        if (droppedWords.Count > 0)
            yield return new WaitForSeconds(0.27f + (droppedWords.Count - 1) * 0.07f);

        transformationInProgress = false;
        inputLocked = false;
        StartCoroutine(CheckRowsAfterTransformation());
    }

    private IEnumerator CheckRowsAfterTransformation()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        foreach (Transform row in wordContainer)
            CheckRow(row);
        RefreshIndicators();

        if (categoriesCompleted >= totalCategoryCount && !transformationInProgress)
            EndGame(true);
        else if (remainingMoves <= 0 && !transformationInProgress)
            EndGame(false);
    }

    private GameObject CreateRuntimeWordButton(BoardWord boardWord, Transform row)
    {
        WordItem word = boardWord.word;
        GameObject newButton = Instantiate(buttonPrefab, row);
        newButton.name = $"Word - {word.text}";

        Image buttonImage = newButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = GetRoundedButtonSprite();
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = PastelButtonColor;
            AddShadow(newButton, new Color(0.08f, 0.08f, 0.22f, 0.48f), new Vector2(0f, -7f));
        }

        TextMeshProUGUI label = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = word.text;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 32;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.16f, 0.12f, 0.28f, 1f);
            if (gameFont != null) label.font = gameFont;
        }

        Sprite wordIcon = null;
        if (word.hasSprite && iconLibrary != null)
            iconLibrary.TryGetIcon(word.spriteKey, out wordIcon);
        newButton.GetComponent<WordButton>().Initialize(this, word.text, boardWord.categoryId,
            PastelButtonColor, wordIcon);
        return newButton;
    }

    private void ShowCompletedCategoryName(Transform row, string categoryId)
    {
        if (row == null || row.Find("CompletedCategoryLabel") != null) return;

        GameObject labelObject = new GameObject("CompletedCategoryLabel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObject.transform.SetParent(row, false);

        LayoutElement layoutElement = labelObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 1f);
        rect.anchorMax = new Vector2(0.92f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 5f);
        rect.sizeDelta = new Vector2(0f, 34f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = categoryNames.TryGetValue(categoryId, out string categoryName)
            ? categoryName.ToUpperInvariant()
            : categoryId.ToUpperInvariant();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 25f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.12f, 0.09f, 0.28f, 1f);
        label.raycastTarget = false;
        if (gameFont != null) label.font = gameFont;
        label.outlineColor = new Color(1f, 1f, 1f, 0.72f);
        label.outlineWidth = 0.12f;

        labelObject.transform.localScale = Vector3.one * 0.7f;
        CanvasGroup group = labelObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        labelObject.transform.DOScale(1f, 0.24f).SetEase(Ease.OutBack);
        group.DOFade(1f, 0.18f);
    }

    private void EndGame(bool won)
    {
        gameEnded = true;
        if (won)
        {
            int lastLevelIndex = levelDatabase == null || levelDatabase.levels == null
                ? activeLevelIndex
                : Mathf.Max(0, levelDatabase.levels.Count - 1);
            int nextLevelIndex = Mathf.Min(activeLevelIndex + 1, lastLevelIndex);
            SaveSystem.CompleteLevel(activeLevelIndex, nextLevelIndex);
            if (goldText != null) goldText.text = SaveSystem.Current.gold.ToString();
            if (winGoldText != null) winGoldText.text = SaveSystem.Current.gold.ToString();
            if (rewardText != null) rewardText.text = "+40 GOLD";
            if (doubleRewardButton != null) doubleRewardButton.interactable = true;
            ShowResultScreen(winScreen);
        }
        else
        {
            UpdateGoldContinueButton();
            ShowResultScreen(loseScreen);
        }
    }

    private void ShowResultScreen(GameObject screen)
    {
        if (screen == null) return;
        screen.SetActive(true);
        screen.transform.SetAsLastSibling();
        screen.transform.localScale = Vector3.one * 0.88f;
        screen.transform.DOKill();
        screen.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void ContinueAfterWin()
    {
        if (winScreen != null) winScreen.SetActive(false);
        int lastLevelIndex = levelDatabase == null || levelDatabase.levels == null
            ? activeLevelIndex
            : Mathf.Max(0, levelDatabase.levels.Count - 1);
        LoadLevel(Mathf.Min(activeLevelIndex + 1, lastLevelIndex));
    }

    private void RetryLevel()
    {
        if (loseScreen != null) loseScreen.SetActive(false);
        LoadLevel(activeLevelIndex);
    }

    private void ContinueLossWithAd()
    {
        ResumeAfterLoss(5);
    }

    private void ContinueLossWithGold()
    {
        int continueCost = GetPaidReviveCost();
        if (!SaveSystem.TrySpendGold(continueCost))
        {
            if (loseExplanationText != null) loseExplanationText.text = "NOT ENOUGH GOLD";
            return;
        }

        paidReviveCount++;
        if (goldText != null) goldText.text = SaveSystem.Current.gold.ToString();
        ResumeAfterLoss(5);
    }

    private int GetPaidReviveCost()
    {
        if (paidReviveCount == 0) return 900;
        if (paidReviveCount == 1) return 1800;
        return 2700;
    }

    private void UpdateGoldContinueButton()
    {
        if (spendGoldContinueButton == null) return;
        TextMeshProUGUI label = spendGoldContinueButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = $"SPEND {GetPaidReviveCost()} GOLD\nCONTINUE";
    }

    private void ResumeAfterLoss(int extraMoves)
    {
        remainingMoves = Mathf.Max(1, extraMoves);
        moveCount = remainingMoves;
        gameEnded = false;
        inputLocked = false;
        if (loseScreen != null) loseScreen.SetActive(false);
        RefreshIndicators();
    }

    private void ViewBoardTemporarily()
    {
        if (loseScreen != null) loseScreen.SetActive(false);
        StartCoroutine(ShowLoseScreenAgain());
    }

    private IEnumerator ShowLoseScreenAgain()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (gameEnded && loseScreen != null) ShowResultScreen(loseScreen);
    }

    private void DoubleWinReward()
    {
        if (rewardDoubled) return;
        rewardDoubled = true;
        SaveSystem.AddGold(40);
        if (rewardText != null) rewardText.text = "+80 GOLD";
        if (winGoldText != null) winGoldText.text = SaveSystem.Current.gold.ToString();
        if (goldText != null) goldText.text = SaveSystem.Current.gold.ToString();
        if (doubleRewardButton != null) doubleRewardButton.interactable = false;
        ContinueAfterWin();
    }

    private void ApplyBoardStyle()
    {
        Image boardBackground = wordContainer.GetComponent<Image>();
        if (boardBackground != null)
        {
            boardBackground.sprite = GetRoundedPanelSprite();
            boardBackground.type = Image.Type.Sliced;
            boardBackground.color = Color.clear;
        }

        Transform backgroundTransform = wordContainer.parent.Find("background");
        Image backgroundImage = backgroundTransform == null ? null : backgroundTransform.GetComponent<Image>();
        Texture2D pastelTexture = Resources.Load<Texture2D>("Textures/PastelBackground");
        if (backgroundImage != null && pastelTexture != null)
        {
            backgroundImage.sprite = Sprite.Create(pastelTexture,
                new Rect(0f, 0f, pastelTexture.width, pastelTexture.height),
                new Vector2(0.5f, 0.5f), 100f);
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = Color.white;
        }
    }

    private void ClearBoard()
    {
        for (int i = wordContainer.childCount - 1; i >= 0; i--)
            DestroyGeneratedObject(wordContainer.GetChild(i).gameObject);
    }

    private void ResizeBoard(int rowCount)
    {
        RectTransform boardRect = wordContainer as RectTransform;
        RectTransform rowRect = rowPrefab.GetComponent<RectTransform>();
        VerticalLayoutGroup layout = wordContainer.GetComponent<VerticalLayoutGroup>();
        if (boardRect == null || rowRect == null) return;

        if (layout != null)
        {
            // Keep the dark WordContainer visible above and below the rows.
            layout.padding = new RectOffset(18, 18, 50, 50);
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        // Four 210px buttons + gaps + panel padding require this width.
        // Applying it at runtime also overrides stale values from an open scene.
        boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1010f);
        float targetBoardHeight = GetBoardHeightForRows(rowCount);
        float boardCenterY = rowCount <= 4 ? 75f : rowCount == 5 ? 10f : -55f;
        boardRect.anchoredPosition = new Vector2(boardRect.anchoredPosition.x, boardCenterY);
        float padding = layout == null ? 0f : layout.padding.vertical;
        float minimumSpacing = 18f;
        float spacing = minimumSpacing;

        if (layout != null && rowCount > 1)
        {
            float freeSpace = targetBoardHeight - padding - rowCount * rowRect.rect.height;
            spacing = Mathf.Max(minimumSpacing, freeSpace / (rowCount - 1));
            layout.spacing = spacing;
        }

        float contentHeight = rowCount * rowRect.rect.height + Mathf.Max(0, rowCount - 1) * spacing + padding;
        float height = Mathf.Max(targetBoardHeight, contentHeight);
        boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private static float GetBoardHeightForRows(int rowCount)
    {
        switch (rowCount)
        {
            case 1: return 300f;
            case 2: return 540f;
            case 3: return 780f;
            case 4: return 1050f;
            case 5: return 1200f;
            case 6: return 1370f;
            default: return 1370f + (rowCount - 6) * 180f;
        }
    }

    private void CreateScreenChrome()
    {
        Canvas canvas = wordContainer.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        canvas.transform.localScale = Vector3.one;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (gameHud == null)
            gameHud = canvas.transform.Find("GameHUD") as RectTransform;
        if (gameHud == null)
        {
            Debug.LogError("GameHUD is missing from the Game scene. Run Tools > Word Game > Setup Game Scene Assets.", this);
            return;
        }

        gameHud.gameObject.SetActive(true);
        ApplySafeArea(gameHud);
        if (progressText == null) progressText = gameHud.Find("TopBar/ProgressCard/Progress")?.GetComponent<TextMeshProUGUI>();
        if (progressFill == null) progressFill = gameHud.Find("TopBar/ProgressCard/ProgressTrack/Fill")?.GetComponent<Image>();
        if (progressKnob == null) progressKnob = gameHud.Find("TopBar/ProgressCard/ProgressTrack/Knob") as RectTransform;
        if (progressKnobText == null) progressKnobText = gameHud.Find("TopBar/ProgressCard/ProgressTrack/Knob/Value")?.GetComponent<TextMeshProUGUI>();
        if (moveCountText == null) moveCountText = gameHud.Find("TopBar/MovesCard/MoveCount")?.GetComponent<TextMeshProUGUI>();
        if (goldText == null) goldText = gameHud.Find("TopBar/ProgressCard/GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
        if (levelLabel == null) levelLabel = gameHud.Find("LevelRibbon/LevelLabel")?.GetComponent<TextMeshProUGUI>();
        if (hintCountText == null) hintCountText = gameHud.Find("BoosterArea/HintButton/CountBadge/Count")?.GetComponent<TextMeshProUGUI>();
        if (hintCountText != null)
            hintCountText.text = (playerSaveData ?? SaveSystem.Current).hintCount.ToString();
        if (goldText != null)
            goldText.text = (playerSaveData ?? SaveSystem.Current).gold.ToString();
        if (winScreen == null) winScreen = gameHud.Find("WinScreen")?.gameObject;
        if (loseScreen == null) loseScreen = gameHud.Find("LoseScreen")?.gameObject;
        if (winGoldText == null) winGoldText = gameHud.Find("WinScreen/Panel/Gold/Value")?.GetComponent<TextMeshProUGUI>();
        if (rewardText == null) rewardText = gameHud.Find("WinScreen/Panel/Reward/Value")?.GetComponent<TextMeshProUGUI>();
        if (doubleRewardButton == null) doubleRewardButton = gameHud.Find("WinScreen/Panel/DoubleRewardButton")?.GetComponent<Button>();
        if (continueButton == null) continueButton = gameHud.Find("WinScreen/Panel/ContinueButton")?.GetComponent<Button>();
        if (retryButton == null) retryButton = gameHud.Find("LoseScreen/Panel/RetryButton")?.GetComponent<Button>();
        if (watchAdContinueButton == null) watchAdContinueButton = gameHud.Find("LoseScreen/Panel/WatchAdContinueButton")?.GetComponent<Button>();
        if (spendGoldContinueButton == null) spendGoldContinueButton = gameHud.Find("LoseScreen/Panel/SpendGoldContinueButton")?.GetComponent<Button>();
        if (viewBoardButton == null) viewBoardButton = gameHud.Find("LoseScreen/ViewBoardButton")?.GetComponent<Button>();
        if (loseExplanationText == null) loseExplanationText = gameHud.Find("LoseScreen/Panel/Explanation")?.GetComponent<TextMeshProUGUI>();
        if (doubleRewardButton != null)
        {
            doubleRewardButton.onClick.RemoveListener(DoubleWinReward);
            doubleRewardButton.onClick.AddListener(DoubleWinReward);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueAfterWin);
            continueButton.onClick.AddListener(ContinueAfterWin);
        }
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryLevel);
            retryButton.onClick.AddListener(RetryLevel);
        }
        if (watchAdContinueButton != null)
        {
            watchAdContinueButton.onClick.RemoveListener(ContinueLossWithAd);
            watchAdContinueButton.onClick.AddListener(ContinueLossWithAd);
        }
        if (spendGoldContinueButton != null)
        {
            spendGoldContinueButton.onClick.RemoveListener(ContinueLossWithGold);
            spendGoldContinueButton.onClick.AddListener(ContinueLossWithGold);
        }
        if (viewBoardButton != null)
        {
            viewBoardButton.onClick.RemoveListener(ViewBoardTemporarily);
            viewBoardButton.onClick.AddListener(ViewBoardTemporarily);
        }
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        gameHud.SetAsLastSibling();
    }

    private void RefreshIndicators()
    {
        if (progressText != null) progressText.text = $"{categoriesCompleted} COMPLETE • {totalCategoryCount} TOTAL";
        float progressRatio = totalCategoryCount <= 0 ? 0f : (float)categoriesCompleted / totalCategoryCount;
        if (progressFill != null) progressFill.fillAmount = progressRatio;
        if (progressKnob != null)
        {
            progressKnob.anchorMin = progressKnob.anchorMax = new Vector2(progressRatio, 0.5f);
            progressKnob.anchoredPosition = Vector2.zero;
        }
        if (progressKnobText != null) progressKnobText.text = categoriesCompleted.ToString();
        if (moveCountText != null) moveCountText.text = $"MOVES\n{moveCount}";
        if (goldText != null) goldText.text = SaveSystem.Current.gold.ToString();
        if (levelLabel != null) levelLabel.text = $"LEVEL {currentLevelNumber}";
        if (progressDotsParent != null && progressDots.Count != currentLevelRows)
            CreateProgressDots(progressDotsParent, currentLevelRows);
        for (int i = 0; i < progressDots.Count; i++)
            progressDots[i].color = i < categoriesCompleted
                ? new Color(1f, 0.55f, 0.45f, 1f)
                : new Color(1f, 1f, 1f, 0.45f);
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.sprite = GetRoundedPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(string objectName, Transform parent, string value, TextAlignmentOptions alignment)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.alignment = alignment;
        label.fontSize = 42;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.86f, 0.64f);
        return label;
    }

    private static void CreateHudButton(string objectName, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, string countBadgeValue = "3")
    {
        Color accent = objectName == "HintButton"
            ? new Color(0.46f, 0.36f, 0.78f, 1f)
            : objectName == "LockButton"
                ? new Color(0.64f, 0.61f, 0.74f, 0.82f)
                : new Color(1.00f, 0.48f, 0.48f, 1f);
        GameObject buttonObject = CreatePanel(objectName, parent, accent);
        buttonObject.GetComponent<Image>().sprite = GetRoundedButtonSprite();
        Button button = buttonObject.AddComponent<Button>();
        button.interactable = objectName != "LockButton";
        AddShadow(buttonObject, new Color(0.18f, 0.10f, 0.32f, 0.22f), new Vector2(0f, -5f));
        SetAnchors(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

        TextMeshProUGUI buttonLabel = CreateLabel("Label", buttonObject.transform, label, TextAlignmentOptions.Center);
        buttonLabel.enableAutoSizing = true;
        buttonLabel.fontSizeMin = 22;
        buttonLabel.fontSizeMax = 34;
        buttonLabel.margin = new Vector4(8f, 4f, 8f, 4f);
        Stretch(buttonLabel.rectTransform);

        if (objectName == "HintButton")
        {
            GameObject iconObject = new GameObject("BulbIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = GetBulbIconSprite();
            icon.preserveAspect = true;
            icon.color = new Color(1f, 0.92f, 0.48f, 1f);
            SetAnchors(icon.rectTransform, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f));
            CreateCountBadge(buttonObject.transform, countBadgeValue);
        }
        else if (objectName == "SettingsButton" || objectName == "LockButton")
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = objectName == "SettingsButton" ? GetGearIconSprite() : GetLockIconSprite();
            icon.preserveAspect = true;
            icon.color = Color.white;
            SetAnchors(icon.rectTransform, new Vector2(0.24f, 0.20f), new Vector2(0.76f, 0.80f));
        }
    }

    private void CreateProgressDots(Transform parent, int count)
    {
        foreach (Image dot in progressDots)
            if (dot != null) Destroy(dot.gameObject);
        progressDots.Clear();
        float totalWidth = Mathf.Min(0.36f, count * 0.045f);
        float start = 0.5f - totalWidth * 0.5f;
        float step = totalWidth / Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            GameObject dot = CreatePanel($"ProgressDot{i + 1}", parent, new Color(1f, 1f, 1f, 0.45f));
            Image image = dot.GetComponent<Image>();
            image.raycastTarget = false;
            SetAnchors(image.rectTransform,
                new Vector2(start + i * step + 0.008f, 0.832f),
                new Vector2(start + (i + 1) * step - 0.008f, 0.842f));
            progressDots.Add(image);
        }
    }

    private static void CreateCategoryIcon(Transform parent)
    {
        Color[] colors =
        {
            new Color(1f, 0.55f, 0.48f),
            new Color(1f, 0.82f, 0.42f),
            new Color(0.53f, 0.88f, 0.78f)
        };

        for (int i = 0; i < 3; i++)
        {
            GameObject layer = CreatePanel($"CategoryLayer{i + 1}", parent, colors[i]);
            Image image = layer.GetComponent<Image>();
            image.raycastTarget = false;
            float y = 0.31f + i * 0.13f;
            SetAnchors(image.rectTransform, new Vector2(0.045f, y), new Vector2(0.105f, y + 0.23f));
        }
    }

    private static void CreateCountBadge(Transform parent, string value)
    {
        GameObject badge = CreatePanel("CountBadge", parent, new Color(1f, 0.48f, 0.48f, 1f));
        SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.66f, 0.64f), new Vector2(1.14f, 1.12f));
        TextMeshProUGUI count = CreateLabel("Count", badge.transform, value, TextAlignmentOptions.Center);
        count.enableAutoSizing = false;
        count.fontSize = 30;
        count.fontStyle = FontStyles.Bold;
        count.color = Color.white;
        count.outlineColor = new Color(0.42f, 0.20f, 0.36f, 0.65f);
        count.outlineWidth = 0.16f;
        Stretch(count.rectTransform);
    }

    private static void CreateDecorativeBubble(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        GameObject bubble = CreatePanel(name, parent, color);
        Image image = bubble.GetComponent<Image>();
        image.raycastTarget = false;
        SetAnchors(image.rectTransform, min, max);
        bubble.transform.SetAsFirstSibling();
    }

    private static void ApplySafeArea(RectTransform rect)
    {
        Rect safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;
        SetAnchors(rect, min, max);
    }

    private static void AddShadow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null) shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite == null) roundedPanelSprite = CreateRoundedSprite(64, 14);
        return roundedPanelSprite;
    }

    private static Sprite GetRoundedButtonSprite()
    {
        if (roundedButtonSprite == null) roundedButtonSprite = CreateRoundedSprite(64, 18);
        return roundedButtonSprite;
    }

    private static Sprite GetBulbIconSprite()
    {
        if (bulbIconSprite != null) return bulbIconSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(64f, 76f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float headDistance = Vector2.Distance(point, center);
                bool head = headDistance <= 38f && y >= 46;
                bool shoulder = y >= 38 && y < 58 && x >= 43 && x <= 85;
                bool neck = y >= 29 && y < 43 && x >= 47 && x <= 81;
                bool baseUpper = y >= 18 && y < 29 && x >= 43 && x <= 85;
                bool baseLower = y >= 10 && y < 19 && x >= 49 && x <= 79;
                bool filled = head || shoulder || neck || baseUpper || baseLower;

                // Soft antialiasing around the round Material-style bulb head.
                float edgeAlpha = Mathf.Clamp01(39f - headDistance);
                float alpha = filled ? (headDistance > 37f && y >= 46 ? edgeAlpha : 1f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        bulbIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return bulbIconSprite;
    }

    private static Sprite GetGearIconSprite()
    {
        if (gearIconSprite != null) return gearIconSprite;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(32f, 32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - center;
                float distance = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x);
                bool ring = distance >= 13f && distance <= 23f;
                bool tooth = distance > 21f && distance < 29f && Mathf.Abs(Mathf.Sin(angle * 4f)) > 0.72f;
                pixels[y * size + x] = (ring || tooth) ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        gearIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return gearIconSprite;
    }

    private static Sprite GetLockIconSprite()
    {
        if (lockIconSprite != null) return lockIconSprite;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 shackleCenter = new Vector2(32f, 39f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float shackleDistance = Vector2.Distance(point, shackleCenter);
                bool shackle = shackleDistance >= 12f && shackleDistance <= 18f && y >= 36;
                bool body = x >= 15 && x <= 49 && y >= 10 && y <= 38;
                pixels[y * size + x] = (shackle || body) ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        lockIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return lockIconSprite;
    }

    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearestX = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float nearestY = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(nearestX, nearestY));
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
