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
    public Transform wordContainer;
    [Tooltip("Rows authored in Game.unity. Gameplay reuses these objects and never instantiates rows at runtime.")]
    [SerializeField] private RowController[] rowPool = new RowController[6];

    [Header("Level")]
    [Min(0)] public int startingLevelIndex = 0;
    [Tooltip("Zero-based level index shown in the Game scene while not playing.")]
    [Min(0)] public int editorPreviewLevelIndex = 0;

    [Header("Editor Scene Preview")]
    [Tooltip("Normalized safe-area minimum used only while editing Game.unity. Runtime uses the real device safe area.")]
    public Vector2 editorPreviewSafeAreaMin = new Vector2(0f, 0.02f);
    [Tooltip("Normalized safe-area maximum used only while editing Game.unity. Lower Y moves the top HUD below a notch/punch hole.")]
    public Vector2 editorPreviewSafeAreaMax = new Vector2(1f, 0.965f);

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
    [SerializeField, Min(0f)] private float winResultDelay = 1.6f;
    [SerializeField, Min(0f)] private float loseResultDelay = 1.2f;

    private readonly List<Image> progressDots = new List<Image>();
    private readonly List<RowController> activeRows = new List<RowController>();
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
    private bool fridgeHudActive;
    private Coroutine endGameRoutine;
    private static Sprite roundedPanelSprite;
    private static Sprite roundedButtonSprite;
    private static Sprite fridgePlainBackgroundSprite;
    private static Sprite fridgeBackgroundSprite;
    private static Sprite fridgeShineFrameSprite;
    private static Sprite fridgeShineLinesSprite;
    private static Sprite fridgeHandleSprite;
    private static Sprite fridgePaperSprite;
    private static Sprite fridgeEggSprite;
    private static Sprite fridgeOrangeSprite;
    private static Sprite fridgeRowSprite;
    private static Sprite fridgeWordSprite;
    private static Sprite fridgeProgressTrackSprite;
    private static Sprite fridgeProgressFillSprite;
    private static Sprite fridgeProgressOrangeSprite;
    private static Sprite fridgeMovesSlotSprite;
    private static Sprite fridgeProgressSlotSprite;
    private static Sprite fridgeSettingsSlotSprite;
    private static Sprite bulbIconSprite;
    private static Sprite gearIconSprite;
    private static Sprite lockIconSprite;
    private WordIconLibrary iconLibrary;
    [Header("Font Assets")]
    [Tooltip("Pre-generated TMP SDF used by words and regular UI text.")]
    [SerializeField] private TMP_FontAsset gameFont;
    [Tooltip("Pre-generated TMP SDF used by the fridge header, moves and level text.")]
    [SerializeField] private TMP_FontAsset displayFont;
    private LevelDatabase levelDatabase;
    private int currentLevelNumber = 1;
    private int totalCategoryCount;
    private int activeLevelIndex;
    private PlayerSaveData playerSaveData;
    private Canvas gameplayCanvas;
    private Vector2 progressKnobScenePosition;
    private float progressKnobSceneAnchorX;
    private bool progressKnobLayoutCached;

    public bool IsInputLocked => inputLocked || gameEnded || transformationInProgress;
    public Canvas GameplayCanvas
    {
        get
        {
            if (gameplayCanvas == null && wordContainer != null)
                gameplayCanvas = wordContainer.GetComponentInParent<Canvas>();
            return gameplayCanvas;
        }
    }

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            ApplyEditorPreviewSafeArea();
        }
    }
#endif

    private void Start()
    {
        if (instance != this) return;
        // The editor hides the generated board while the game is stopped.
        // Always restore it when gameplay starts.
        if (wordContainer != null) wordContainer.gameObject.SetActive(true);
        playerSaveData = SaveSystem.Load();
        LoadGameFont();
        BindScreenChrome();
        LoadLevel(Mathf.Max(startingLevelIndex, playerSaveData.currentLevelIndex));
    }

    public void LoadLevel(int levelIndex)
    {
        if (endGameRoutine != null)
        {
            StopCoroutine(endGameRoutine);
            endGameRoutine = null;
        }

        if (wordContainer == null || rowPool == null || rowPool.Length == 0 || rowPool.Any(row => row == null))
        {
            Debug.LogError("TableController needs its scene-authored Row Pool references. Run Tools > Word Game > Bake Rows Into Game Scene.", this);
            return;
        }

        levelDatabase = LevelDatabase.LoadFromResources();
        iconLibrary = Resources.Load<WordIconLibrary>("Data/MainWordIconLibrary");
        LoadGameFont();
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
        if (currentLevelRows > rowPool.Length)
        {
            Debug.LogError($"Level {level.levelNumber} needs {currentLevelRows} visible rows, but Game.unity contains only {rowPool.Length}. Bake a larger row pool in the scene.", this);
            return;
        }
        int visibleWordCount = currentLevelRows * 4;

        List<BoardWord> shuffledWords = allPlayableWords.Take(visibleWordCount).ToList();

        pendingWords.Clear();
        foreach (BoardWord queuedWord in allPlayableWords.Skip(visibleWordCount))
            pendingWords.Enqueue(queuedWord);

        const int columns = 4;
        for (int rowIndex = 0; rowIndex < currentLevelRows; rowIndex++)
        {
            RowController rowController = rowPool[rowIndex];
            rowController.gameObject.SetActive(true);
            if (!rowController.HasFourSlots())
            {
                Debug.LogError($"{rowController.name} needs four assigned WordButton slots. Run Tools > Word Game > Setup Row Prefab, then bake the rows again.", rowController);
                return;
            }

            WordButtonStyleSettings matchStyle = WordButtonStyleSettings.LoadDefault();
            Color rowMatchColor = matchStyle == null
                ? new Color(0.05f, 0.82f, 0.43f, 1f)
                : matchStyle.GetRowMatchColor(rowIndex);
            Color secondaryColor = matchStyle == null
                ? Color.HSVToRGB(((rowIndex + 7) * 0.618034f) % 1f, 0.55f, 0.9f)
                : matchStyle.GetRowMatchColor(rowIndex, true);
            rowController.PrepareForLevel(this, rowMatchColor, secondaryColor);
            activeRows.Add(rowController);

            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                BoardWord boardWord = shuffledWords[rowIndex * columns + columnIndex];
                WordItem word = boardWord.word;
                Sprite wordIcon = null;
                if (word.useIcon && iconLibrary != null)
                    iconLibrary.TryGetIcon(word.spriteKey, out wordIcon);

                if (word.useIcon && wordIcon == null)
                    Debug.LogWarning($"Icon not found for key '{word.spriteKey}'. Falling back to text.", this);

                rowController.SetWord(columnIndex, this, word.text, boardWord.categoryId, Color.white, wordIcon);
            }
        }

        ResizeBoard(currentLevelRows);
        foreach (RowController row in activeRows) row.ApplyMatchingWordStyle();
        RefreshIndicators();
    }

    private void LoadGameFont()
    {
        // Font atlases are authored once in the Editor and stored as project
        // assets. Never call TMP_FontAsset.CreateFontAsset at runtime.
        if (gameFont == null)
            gameFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (displayFont == null)
            displayFont = Resources.Load<TMP_FontAsset>("Fonts/FredokaOne-Regular SDF");

        if (gameFont == null)
            gameFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (displayFont == null) displayFont = gameFont;
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
        Canvas canvas = GameplayCanvas;
        if (canvas == null) return null;

        WordButton ghost = Instantiate(source, canvas.transform, true);
        ghost.name = $"{source.name} Ghost";
        ghost.ConfigureAsGhost();
        Shadow liftShadow = ghost.gameObject.AddComponent<Shadow>();
        liftShadow.effectColor = new Color(0.08f, 0.07f, 0.12f, 0.42f);
        liftShadow.effectDistance = new Vector2(0f, -8f);
        liftShadow.useGraphicAlpha = true;
        ghost.transform.SetAsLastSibling();
        ghost.transform.DOKill();
        return ghost.RectTransform;
    }

    public void TrySwapWords(WordButton first, WordButton second, Vector3? firstVisualStart = null)
    {
        if (first == null || second == null || first == second || first.IsMatched || second.IsMatched)
        {
            inputLocked = false;
            return;
        }

        inputLocked = true;
        RowController firstRow = first.Row;
        RowController secondRow = second.Row;
        if (firstRow == null || secondRow == null)
        {
            inputLocked = false;
            return;
        }
        bool changedRows = firstRow != secondRow;

        Vector3 firstStart = firstVisualStart ?? first.transform.position;
        Vector3 secondStart = second.transform.position;

        Vector3 firstTarget = second.transform.position;
        Vector3 secondTarget = first.transform.position;
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
            Destroy(firstGhost.gameObject);
            Destroy(secondGhost.gameObject);
            FinishSwap(first, second, firstRow, secondRow, changedRows);
            first.SetVisualAlpha(1f);
            second.SetVisualAlpha(1f);
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

    private void FinishSwap(WordButton first, WordButton second, RowController firstRow, RowController secondRow, bool changedRows)
    {
        first.SwapContentWith(second);
        firstRow.LastArrivedWord = first;
        secondRow.LastArrivedWord = second;
        // Every successful swap is a move, including swaps within one row.
        remainingMoves = Mathf.Max(0, remainingMoves - 1);
        moveCount = remainingMoves;

        // Refresh both sides before completion can start a blocking transformation.
        firstRow.ApplyMatchingWordStyle();
        if (secondRow != firstRow) secondRow.ApplyMatchingWordStyle();
        CheckRow(firstRow);
        if (secondRow != firstRow) CheckRow(secondRow);
        RefreshIndicators();
        inputLocked = false;

        if (categoriesCompleted >= totalCategoryCount && !transformationInProgress)
            EndGame(true);
        else if (remainingMoves <= 0 && !transformationInProgress)
            EndGame(false);
    }

    private void CheckRow(RowController row)
    {
        if (transformationInProgress || row == null || row.IsCompleted) return;
        WordButton[] words = row.GetActiveWords();
        if (words.Length != 4) return;

        string categoryId = words[0].CategoryId;
        if (words.All(word => word.CategoryId == categoryId))
        {
            bool transforms = categoryDefinitions.TryGetValue(categoryId, out Category category) &&
                              category.transformsOnComplete;

            categoriesCompleted++;
            if (transforms)
                TransformCompletedCategory(row, words, category);
            else
                ShowCompletedCategoryName(row, categoryId);
            return;
        }

        row.ApplyMatchingWordStyle();
    }

    private void TransformCompletedCategory(RowController row, WordButton[] words, Category category)
    {
        if (category.transformResult == null) return;
        StartCoroutine(AnimateCategoryTransformation(row, words, category));
    }

    private IEnumerator AnimateCategoryTransformation(RowController row, WordButton[] words, Category category)
    {
        transformationInProgress = true;
        inputLocked = true;

        // Complete the left-to-right row cascade before starting the merge.
        Canvas.ForceUpdateCanvases();
        yield return row.AnimateCompletedStyle(colorHolder: false);

        WordButton survivor = words.Contains(row.LastArrivedWord) ? row.LastArrivedWord : words[words.Length - 1];
        int resultSlot = System.Array.IndexOf(row.WordSlots.ToArray(), survivor);
        Vector3 mergePoint = survivor.transform.position;
        // Use the authored button's local height, so the visible stack scales
        // consistently with the Canvas and device resolution.
        Vector3 stackStep = survivor.transform.TransformVector(
            Vector3.up * survivor.RectTransform.rect.height * 0.085f);
        Vector3 stackTop = mergePoint + stackStep * 2f;

        List<RectTransform> mergeGhosts = new List<RectTransform>();
        Sequence mergeSequence = DOTween.Sequence();
        foreach (WordButton word in words)
        {
            if (word == survivor) continue;
            RectTransform ghost = CreateWordGhost(word);
            if (ghost != null)
            {
                ghost.DOKill();
                mergeGhosts.Add(ghost);
                Vector3 layerPosition = stackTop - stackStep * mergeGhosts.Count;
                mergeSequence.Insert((mergeGhosts.Count - 1) * 0.07f,
                    ghost.DOMove(layerPosition, 0.36f).SetEase(Ease.InOutCubic));
            }
            word.SetTileVisualVisible(false);
        }

        // The last-arrived card must remain readable ON TOP of the other
        // cards; a real slot would render behind ghosts on the overlay Canvas.
        for (int index = mergeGhosts.Count - 1; index >= 0; index--)
            mergeGhosts[index].SetAsLastSibling();
        RectTransform topGhost = CreateWordGhost(survivor);
        if (topGhost != null)
        {
            topGhost.DOKill();
            topGhost.SetAsLastSibling();
            mergeSequence.Insert(0f, topGhost.DOMove(stackTop, 0.18f).SetEase(Ease.OutCubic));
            survivor.SetTileVisualVisible(false);
        }

        yield return mergeSequence.WaitForCompletion();
        yield return new WaitForSeconds(0.18f);
        // Collapse the bottom layers into the top card one by one, rather
        // than making all four overlap perfectly and vanish in one frame.
        for (int index = mergeGhosts.Count - 1; index >= 0; index--)
        {
            RectTransform ghost = mergeGhosts[index];
            if (ghost == null) continue;
            yield return ghost.DOMove(stackTop, 0.09f).SetEase(Ease.InCubic).WaitForCompletion();
            Destroy(ghost.gameObject);
        }
        if (topGhost != null)
        {
            yield return topGhost.DOMove(mergePoint, 0.12f).SetEase(Ease.InOutCubic).WaitForCompletion();
            Vector3 restingScale = topGhost.localScale;
            yield return topGhost.DOScale(restingScale * 0.86f, 0.10f)
                .SetEase(Ease.InCubic).WaitForCompletion();
            Destroy(topGhost.gameObject);
        }
        row.ClearWords();

        BoardWord result = new BoardWord
        {
            word = category.transformResult,
            categoryId = category.transformResultCategoryId
        };
        WordButton transformed = SetRowWord(row, resultSlot, result);
        if (transformed == null)
        {
            transformationInProgress = false;
            inputLocked = false;
            yield break;
        }
        transformed.transform.localScale = Vector3.one * 0.86f;
        row.LastArrivedWord = transformed;

        List<WordButton> droppedWords = new List<WordButton>();
        int openSlots = Mathf.Max(0, 4 - row.ActiveWordCount);
        for (int slot = 0; slot < openSlots && pendingWords.Count > 0; slot++)
        {
            BoardWord nextWord = pendingWords.Dequeue();
            int emptySlot = row.GetFirstEmptySlotIndex();
            WordButton droppedWord = SetRowWord(row, emptySlot, nextWord);
            if (droppedWord == null) break;
            row.LastArrivedWord = droppedWord;
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
        foreach (RowController row in activeRows)
            CheckRow(row);
        RefreshIndicators();

        if (categoriesCompleted >= totalCategoryCount && !transformationInProgress)
            EndGame(true);
        else if (remainingMoves <= 0 && !transformationInProgress)
            EndGame(false);
    }

    private WordButton SetRowWord(RowController row, int slotIndex, BoardWord boardWord)
    {
        if (row == null || slotIndex < 0 || boardWord == null || boardWord.word == null) return null;

        WordItem word = boardWord.word;
        Sprite wordIcon = null;
        if (word.useIcon && iconLibrary != null)
            iconLibrary.TryGetIcon(word.spriteKey, out wordIcon);
        if (word.useIcon && wordIcon == null)
            Debug.LogWarning($"Icon not found for key '{word.spriteKey}'. Falling back to text.", this);

        return row.SetWord(slotIndex, this, word.text, boardWord.categoryId, Color.white, wordIcon);
    }

    private void ShowCompletedCategoryName(RowController row, string categoryId)
    {
        if (row == null) return;
        string categoryName = categoryNames.TryGetValue(categoryId, out string name)
            ? name
            : categoryId;
        row.CompleteCategory(categoryName);
    }

    private void EndGame(bool won)
    {
        if (gameEnded || endGameRoutine != null) return;

        gameEnded = true;
        inputLocked = true;
        float delay = won ? winResultDelay : loseResultDelay;
        endGameRoutine = StartCoroutine(ShowEndGameAfterDelay(won, delay));
    }

    private IEnumerator ShowEndGameAfterDelay(bool won, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        endGameRoutine = null;
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
        Sprite fridgeBackground = GetFridgePlainBackgroundSprite();
        Texture2D pastelTexture = fridgeBackground == null
            ? Resources.Load<Texture2D>("Textures/PastelBackground")
            : null;
        if (backgroundImage != null && (fridgeBackground != null || pastelTexture != null))
        {
            backgroundImage.sprite = fridgeBackground != null
                ? fridgeBackground
                : Sprite.Create(pastelTexture,
                    new Rect(0f, 0f, pastelTexture.width, pastelTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Creates the non-dynamic fridge UI in Game.unity so it can be edited in
    /// the Inspector. Gameplay rows and completed-category visuals remain
    /// runtime generated because their count/content changes per level.
    /// </summary>
    public void BakeStaticGameSceneAssets()
    {
        if (wordContainer == null) return;

        Canvas canvas = wordContainer.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        if (gameHud == null)
            gameHud = canvas.transform.Find("GameHUD") as RectTransform;
        if (gameHud == null) return;

        ApplyEditorPreviewSafeArea();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        LoadGameFont();

        Transform backgroundTransform = wordContainer.parent.Find("background");
        Image backgroundImage = backgroundTransform == null
            ? null
            : backgroundTransform.GetComponent<Image>();
        if (backgroundImage != null)
        {
            Sprite fridgeBackground = GetFridgePlainBackgroundSprite();
            if (fridgeBackground != null)
            {
                backgroundImage.sprite = fridgeBackground;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = Color.white;
                backgroundImage.raycastTarget = false;
                ApplyFridgeBackgroundLayers(backgroundImage);
            }
        }

        Transform previousHud = gameHud.Find("FridgeTopHud");
        // Missing background decorations must never replace the artist-authored HUD.
        if (previousHud == null) CreateFridgeTopHud();
        Transform fridgeHud = gameHud.Find("FridgeTopHud");
        if (fridgeHud != null) fridgeHud.gameObject.SetActive(true);
        GameObject bakeMarker = new GameObject("StaticFridgeSceneV2");
        bakeMarker.transform.SetParent(gameHud, false);
        bakeMarker.SetActive(false);
        BindSceneUiReferences();
    }

    [ContextMenu("Refresh Editor Level Preview")]
    public void BakeEditorLevelPreview()
    {
        if (Application.isPlaying || wordContainer == null) return;

        playerSaveData = SaveSystem.Current;
        wordContainer.gameObject.SetActive(true);
        if (gameHud == null)
        {
            Canvas canvas = wordContainer.GetComponentInParent<Canvas>(true);
            gameHud = canvas == null ? null : canvas.transform.Find("GameHUD") as RectTransform;
        }
        if (gameHud != null)
        {
            ApplyEditorPreviewSafeArea();
            Transform fridgeHud = gameHud.Find("FridgeTopHud");
            if (fridgeHud != null) fridgeHud.gameObject.SetActive(true);
        }
        BindSceneUiReferences();
        LoadLevel(Mathf.Max(0, editorPreviewLevelIndex));
    }

    public bool HasBakedStaticGameSceneAssets()
    {
        if (wordContainer == null) return false;
        Canvas canvas = wordContainer.GetComponentInParent<Canvas>(true);
        Transform background = canvas == null ? null : canvas.transform.Find("background");
        Transform hud = canvas == null ? null : canvas.transform.Find("GameHUD/FridgeTopHud");
        Transform bakeMarker = canvas == null ? null : canvas.transform.Find("GameHUD/StaticFridgeSceneV2");
        Transform gameHudRoot = canvas == null ? null : canvas.transform.Find("GameHUD");
        return gameHudRoot != null &&
               gameHudRoot.gameObject.activeSelf &&
               hud != null &&
               hud.gameObject.activeSelf &&
               bakeMarker != null &&
               background != null &&
               background.Find("FridgeGradient") != null &&
               background.Find("FridgeShineLines") != null &&
               background.Find("FridgeShineFrame") != null &&
               background.Find("FridgeHandle") != null &&
               background.Find("BottomRightDecoration") != null;
    }

    /// <summary>
    /// Applies a stable, editable safe-area preview to Game.unity. This avoids
    /// serializing transient Device Simulator values while keeping Edit Mode
    /// visually aligned with the selected phone profile.
    /// </summary>
    public bool ApplyEditorPreviewSafeArea()
    {
        if (gameHud == null && wordContainer != null)
        {
            Canvas canvas = wordContainer.GetComponentInParent<Canvas>(true);
            gameHud = canvas == null ? null : canvas.transform.Find("GameHUD") as RectTransform;
        }
        if (gameHud == null) return false;

        Vector2 min = new Vector2(
            Mathf.Clamp01(editorPreviewSafeAreaMin.x),
            Mathf.Clamp01(editorPreviewSafeAreaMin.y));
        Vector2 max = new Vector2(
            Mathf.Clamp(editorPreviewSafeAreaMax.x, min.x + 0.01f, 1f),
            Mathf.Clamp(editorPreviewSafeAreaMax.y, min.y + 0.01f, 1f));

        bool changed = gameHud.anchorMin != min ||
                       gameHud.anchorMax != max ||
                       gameHud.offsetMin != Vector2.zero ||
                       gameHud.offsetMax != Vector2.zero ||
                       !gameHud.gameObject.activeSelf;

        SetAnchors(gameHud, min, max);
        gameHud.gameObject.SetActive(true);
        gameHud.SetAsLastSibling();
        return changed;
    }
#endif

    private void ApplyFridgeBackgroundLayers(Image backgroundImage)
    {
        Transform root = backgroundImage.transform;
        AddStretchBackgroundLayer(root, "FridgeGradient", GetFridgeBackgroundSprite(), 0);
        AddStretchBackgroundLayer(root, "FridgeShineLines", GetFridgeShineLinesSprite(), 1);
        AddStretchBackgroundLayer(root, "FridgeShineFrame", GetFridgeShineFrameSprite(), 2);

        AddDecorativeBackgroundLayer(root, "FridgeHandle", GetFridgeHandleSprite(),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30f, 70f), new Vector2(480f, 88f), 3);
        Transform eggDecoration = root.Find("EggMagnet");
        if (eggDecoration != null) eggDecoration.gameObject.SetActive(false);
        CreateBottomRightDecoration(root);
    }

    private static void CreateBottomRightDecoration(Transform root)
    {
        Transform existing = root.Find("BottomRightDecoration");
        GameObject group = existing == null
            ? new GameObject("BottomRightDecoration", typeof(RectTransform))
            : existing.gameObject;
        if (existing == null) group.transform.SetParent(root, false);

        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = groupRect.anchorMax = new Vector2(1f, 0f);
        groupRect.pivot = new Vector2(1f, 0f);
        groupRect.anchoredPosition = new Vector2(-35f, 190f);
        groupRect.sizeDelta = new Vector2(200f, 180f);
        group.transform.SetSiblingIndex(Mathf.Min(5, root.childCount - 1));

        Image paper = GetOrCreateBackgroundImage(group.transform, "Paper");
        paper.sprite = GetFridgePaperSprite();
        paper.type = Image.Type.Simple;
        paper.preserveAspect = true;
        paper.color = Color.white;
        RectTransform paperRect = paper.rectTransform;
        paperRect.anchorMin = paperRect.anchorMax = new Vector2(0f, 0f);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperRect.anchoredPosition = new Vector2(60f, 60f);
        paperRect.sizeDelta = new Vector2(118f, 118f);
        paper.transform.SetAsFirstSibling();

        Image orange = GetOrCreateBackgroundImage(group.transform, "Orange");
        orange.sprite = GetFridgeOrangeSprite();
        orange.type = Image.Type.Simple;
        orange.preserveAspect = true;
        orange.color = Color.white;
        RectTransform orangeRect = orange.rectTransform;
        orangeRect.anchorMin = orangeRect.anchorMax = new Vector2(0f, 0f);
        orangeRect.pivot = new Vector2(0.5f, 0.5f);
        orangeRect.anchoredPosition = new Vector2(99f, 115f);
        orangeRect.sizeDelta = new Vector2(112f, 132f);
        orange.transform.SetAsLastSibling();
    }

    private static void AddStretchBackgroundLayer(Transform parent, string name, Sprite sprite, int siblingIndex)
    {
        if (sprite == null) return;
        Image image = GetOrCreateBackgroundImage(parent, name);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        RectTransform rect = image.rectTransform;
        Stretch(rect);
        rect.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
    }

    private static void AddDecorativeBackgroundLayer(Transform parent, string name, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, int siblingIndex)
    {
        if (sprite == null) return;
        Image image = GetOrCreateBackgroundImage(parent, name);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.SetSiblingIndex(Mathf.Min(siblingIndex, parent.childCount - 1));
    }

    private static Image GetOrCreateBackgroundImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject layer = existing == null
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : existing.gameObject;
        if (existing == null) layer.transform.SetParent(parent, false);
        Image image = layer.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void ClearBoard()
    {
        activeRows.Clear();
        if (rowPool == null) return;
        foreach (RowController row in rowPool)
        {
            if (row == null) continue;
            row.ClearWords();
            row.gameObject.SetActive(false);
        }
    }

    private void ResizeBoard(int rowCount)
    {
        RectTransform boardRect = wordContainer as RectTransform;
        RectTransform rowRect = rowPool != null && rowPool.Length > 0 && rowPool[0] != null
            ? rowPool[0].transform as RectTransform
            : null;
        VerticalLayoutGroup layout = wordContainer.GetComponent<VerticalLayoutGroup>();
        if (boardRect == null || rowRect == null) return;

        if (layout != null)
        {
            // Keep the dark WordContainer visible above and below the rows.
            layout.padding = new RectOffset(18, 18, 50, 50);
            layout.childAlignment = TextAnchor.UpperCenter;
            // Keep the holder's own aspect ratio; do not stretch rows to the
            // WordContainer width because the four painted slots then drift.
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
        }

        bool usesFridgeArt = GetFridgeRowSprite() != null;
        // Four buttons + gaps + panel padding require this width.
        // Applying it at runtime also overrides stale values from an open scene.
        boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, usesFridgeArt ? 950f : 1010f);
        float rowHeight = usesFridgeArt ? 160f : rowRect.rect.height;
        float targetBoardHeight = usesFridgeArt
            ? GetFridgeBoardHeightForRows(rowCount)
            : GetBoardHeightForRows(rowCount);
        float boardCenterY = usesFridgeArt
            ? (rowCount <= 4 ? 100f : rowCount == 5 ? 55f : 10f)
            : (rowCount <= 4 ? 75f : rowCount == 5 ? 10f : -55f);
        boardRect.anchoredPosition = new Vector2(boardRect.anchoredPosition.x, boardCenterY);
        float padding = layout == null ? 0f : layout.padding.vertical;
        float minimumSpacing = 18f;
        float spacing = minimumSpacing;

        if (layout != null && rowCount > 1)
        {
            float freeSpace = targetBoardHeight - padding - rowCount * rowHeight;
            spacing = Mathf.Max(minimumSpacing, freeSpace / (rowCount - 1));
            layout.spacing = spacing;
        }

        float contentHeight = rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * spacing + padding;
        float height = Mathf.Max(targetBoardHeight, contentHeight);
        boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (usesFridgeArt)
        {
            foreach (RowController liveRow in activeRows)
                if (liveRow != null && liveRow.transform is RectTransform rowTransform)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rowTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(boardRect);
        }
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

    private void BindScreenChrome()
    {
        Canvas canvas = wordContainer.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (gameHud == null)
            gameHud = canvas.transform.Find("GameHUD") as RectTransform;
        if (gameHud == null)
        {
            Debug.LogError("GameHUD is missing from the Game scene. Run Tools > Word Game > Setup Game Scene Assets.", this);
            return;
        }

        gameHud.gameObject.SetActive(true);
        BindSceneUiReferences();
        if (gameHud.Find("FridgeTopHud") == null)
        {
            Debug.LogError("FridgeTopHud is missing from Game.unity. Run Tools > Word Game > Setup Game Scene Assets.", this);
            return;
        }
        fridgeHudActive = true;
        WireSceneButtons();
        if (hintCountText != null)
            hintCountText.text = (playerSaveData ?? SaveSystem.Current).hintCount.ToString();
        if (goldText != null)
            goldText.text = (playerSaveData ?? SaveSystem.Current).gold.ToString();
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
    }

    private void BindSceneUiReferences()
    {
        if (progressText == null) progressText = gameHud.Find("TopBar/ProgressCard/Progress")?.GetComponent<TextMeshProUGUI>();
        // Always prefer the visible fridge HUD. Older Game scenes can still
        // contain serialized references to the hidden legacy TopBar, which
        // makes the correct values update off-screen.
        Image visibleProgressFill = gameHud.Find("FridgeTopHud/ProgressCard/ProgressTrack/Fill")?.GetComponent<Image>();
        RectTransform visibleProgressKnob = gameHud.Find("FridgeTopHud/ProgressCard/ProgressTrack/Knob") as RectTransform;
        TextMeshProUGUI visibleProgressKnobText = gameHud.Find("FridgeTopHud/ProgressCard/ProgressTrack/Knob/Value")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI visibleMoveCount = gameHud.Find("FridgeTopHud/MovesMagnet/MoveCount")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI visibleLevelLabel = gameHud.Find("FridgeTopHud/LevelLabel")?.GetComponent<TextMeshProUGUI>();
        if (visibleProgressFill != null) progressFill = visibleProgressFill;
        if (visibleProgressKnob != null) progressKnob = visibleProgressKnob;
        if (visibleProgressKnobText != null) progressKnobText = visibleProgressKnobText;
        if (visibleMoveCount != null) moveCountText = visibleMoveCount;
        if (visibleLevelLabel != null) levelLabel = visibleLevelLabel;
        CacheProgressKnobSceneLayout();
        if (goldText == null) goldText = gameHud.Find("TopBar/ProgressCard/GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
        if (hintCountText == null) hintCountText = gameHud.Find("BoosterArea/HintButton/CountBadge/Count")?.GetComponent<TextMeshProUGUI>();
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
    }

    private void WireSceneButtons()
    {
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
    }

    private void CacheProgressKnobSceneLayout()
    {
        if (progressKnobLayoutCached || progressKnob == null) return;
        progressKnobScenePosition = progressKnob.anchoredPosition;
        progressKnobSceneAnchorX = progressKnob.anchorMin.x;
        progressKnobLayoutCached = true;
    }

    private void CreateFridgeTopHud()
    {
        Transform oldTopBar = gameHud.Find("TopBar");
        if (oldTopBar != null) oldTopBar.gameObject.SetActive(false);
        SetHudObjectActive("RibbonLeft", false);
        SetHudObjectActive("RibbonRight", false);
        SetHudObjectActive("LevelRibbon", false);
        SetHudObjectActive("BoosterArea", false);

        Transform existing = gameHud.Find("FridgeTopHud");
        GameObject hudObject = existing == null ? CreateRect("FridgeTopHud", gameHud) : existing.gameObject;
        RectTransform hudRect = hudObject.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(1f, 1f);
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.anchoredPosition = Vector2.zero;
        hudRect.sizeDelta = new Vector2(0f, 285f);

        TextMeshProUGUI movesTitle = CreateLabel("MovesTitle", hudRect, "moves", TextAlignmentOptions.Center);
        SetAbsoluteTopRect(movesTitle.rectTransform, new Vector2(38f, -8f), new Vector2(190f, 58f), new Vector2(0f, 1f));
        StyleFridgeTitle(movesTitle, 48f);
        movesTitle.color = new Color(0.52f, 0.31f, 0.66f, 1f);

        GameObject movesCardObject = CreatePanel("MovesMagnet", hudRect, Color.white);
        Image movesCard = movesCardObject.GetComponent<Image>();
        movesCard.sprite = GetFridgeMovesSlotSprite();
        movesCard.type = Image.Type.Simple;
        movesCard.color = Color.white;
        SetAbsoluteTopRect(movesCard.rectTransform, new Vector2(55f, -73f), new Vector2(145f, 92f), new Vector2(0f, 1f));
        moveCountText = CreateLabel("MoveCount", movesCard.transform, "0", TextAlignmentOptions.Center);
        moveCountText.fontSize = 48f;
        moveCountText.color = new Color(0.42f, 0.30f, 0.14f, 1f);
        Stretch(moveCountText.rectTransform);

        levelLabel = CreateLabel("LevelLabel", hudRect, "Level <color=#D69535>1</color>", TextAlignmentOptions.Center);
        levelLabel.rectTransform.anchorMin = new Vector2(0.27f, 1f);
        levelLabel.rectTransform.anchorMax = new Vector2(0.73f, 1f);
        levelLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        levelLabel.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        levelLabel.rectTransform.sizeDelta = new Vector2(0f, 70f);
        StyleFridgeTitle(levelLabel, 64f);

        GameObject progressCardObject = CreatePanel(
            "ProgressCard",
            hudRect,
            Color.white);
        progressCardObject.GetComponent<Image>().sprite = GetFridgeProgressSlotSprite();
        progressCardObject.GetComponent<Image>().type = Image.Type.Simple;
        RectTransform progressCardRect = progressCardObject.GetComponent<RectTransform>();
        progressCardRect.anchorMin = progressCardRect.anchorMax = new Vector2(0.5f, 1f);
        progressCardRect.pivot = new Vector2(0.5f, 1f);
        progressCardRect.anchoredPosition = new Vector2(0f, -108f);
        progressCardRect.sizeDelta = new Vector2(472f, 105f);
        progressCardObject.GetComponent<Image>().raycastTarget = false;

        GameObject trackObject = CreateRect("ProgressTrack", progressCardRect);
        Image trackImage = trackObject.AddComponent<Image>();
        trackImage.sprite = GetFridgeProgressTrackSprite();
        trackImage.type = Image.Type.Simple;
        trackImage.color = Color.white;
        trackImage.raycastTarget = false;
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.anchoredPosition = new Vector2(0f, -2f);
        trackRect.sizeDelta = new Vector2(405f, 50f);

        GameObject fillObject = CreateRect("Fill", trackRect);
        progressFill = fillObject.AddComponent<Image>();
        progressFill.sprite = GetFridgeProgressFillSprite();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.color = Color.white;
        progressFill.raycastTarget = false;
        Stretch(progressFill.rectTransform);
        // Extend the green fill beneath the orange so the track sprite's dark
        // left cap never peeks through as the orange moves away.
        progressFill.rectTransform.offsetMin = new Vector2(-12f, 0f);
        progressFill.rectTransform.offsetMax = Vector2.zero;

        GameObject knobObject = CreateRect("Knob", trackRect);
        Image knobImage = knobObject.AddComponent<Image>();
        knobImage.sprite = GetFridgeProgressOrangeSprite();
        knobImage.type = Image.Type.Simple;
        knobImage.preserveAspect = true;
        knobImage.raycastTarget = false;
        progressKnob = knobObject.GetComponent<RectTransform>();
        progressKnob.anchorMin = progressKnob.anchorMax = new Vector2(0f, 0.5f);
        progressKnob.pivot = new Vector2(0.5f, 0.5f);
        progressKnob.anchoredPosition = Vector2.zero;
        progressKnob.sizeDelta = new Vector2(58f, 64f);
        progressKnobText = CreateLabel("Value", progressKnob, "0", TextAlignmentOptions.Center);
        progressKnobText.fontSize = 22f;
        progressKnobText.color = Color.white;
        SetAnchors(progressKnobText.rectTransform, new Vector2(0.22f, 0.13f), new Vector2(0.78f, 0.70f));

        GameObject settingsObject = CreatePanel("SettingsButton", hudRect, Color.white);
        settingsObject.GetComponent<Image>().sprite = GetFridgeSettingsSlotSprite();
        settingsObject.GetComponent<Image>().type = Image.Type.Simple;
        RectTransform settingsRect = settingsObject.GetComponent<RectTransform>();
        SetAbsoluteTopRect(settingsRect, new Vector2(-38f, -25f), new Vector2(135f, 135f), new Vector2(1f, 1f));
        settingsRect.pivot = new Vector2(1f, 1f);
        Button settingsButton = settingsObject.AddComponent<Button>();
        settingsButton.targetGraphic = settingsObject.GetComponent<Image>();
        AddShadow(settingsObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -6f));
        GameObject gearObject = CreateRect("Gear", settingsRect);
        Image gear = gearObject.AddComponent<Image>();
        gear.sprite = GetGearIconSprite();
        gear.preserveAspect = true;
        gear.color = new Color(0.28f, 0.31f, 0.34f, 1f);
        gear.raycastTarget = false;
        SetAnchors(gear.rectTransform, new Vector2(0.20f, 0.18f), new Vector2(0.80f, 0.82f));

        progressText = null;
        goldText = null;
        fridgeHudActive = true;
        hudObject.transform.SetAsLastSibling();
    }

    private void SetHudObjectActive(string objectName, bool active)
    {
        Transform item = gameHud.Find(objectName);
        if (item != null) item.gameObject.SetActive(active);
    }

    private void StyleFridgeTitle(TextMeshProUGUI label, float fontSize)
    {
        label.color = new Color(0.72f, 0.25f, 0.62f, 1f);
        // Font asset and material are authored in the scene. In particular,
        // preserve its size, weight/style and variable-font shadow material.
        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null) shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.20f, 0.18f, 0.30f, 0.48f);
        shadow.effectDistance = new Vector2(2f, -4f);
        shadow.useGraphicAlpha = true;
    }

    private static void SetAbsoluteTopRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ApplyFridgeProgressArt()
    {
        Sprite trackSprite = GetFridgeProgressTrackSprite();
        Sprite fillSprite = GetFridgeProgressFillSprite();
        Transform trackTransform = gameHud == null ? null : gameHud.Find("TopBar/ProgressCard/ProgressTrack");
        Image trackImage = trackTransform == null ? null : trackTransform.GetComponent<Image>();

        if (trackImage != null && trackSprite != null)
        {
            trackImage.sprite = trackSprite;
            trackImage.type = Image.Type.Simple;
            trackImage.color = Color.white;
        }

        if (progressFill != null && fillSprite != null)
        {
            progressFill.sprite = fillSprite;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.color = Color.white;
        }
    }

    private static float GetFridgeBoardHeightForRows(int rowCount)
    {
        switch (rowCount)
        {
            case 1: return 320f;
            case 2: return 560f;
            case 3: return 800f;
            case 4: return 1040f;
            case 5: return 1230f;
            case 6: return 1430f;
            default: return 1430f + (rowCount - 6) * 180f;
        }
    }

    private void RefreshIndicators()
    {
        if (progressText != null) progressText.text = $"{categoriesCompleted} COMPLETE • {totalCategoryCount} TOTAL";
        float progressRatio = totalCategoryCount <= 0 ? 0f : (float)categoriesCompleted / totalCategoryCount;
        if (progressFill != null) progressFill.fillAmount = progressRatio;
        // Move only along the authored track. Size, anchors, vertical position
        // and the Scene-authored starting offset remain untouched.
        if (progressKnob != null && progressKnob.parent is RectTransform progressTrack)
        {
            CacheProgressKnobSceneLayout();
            float travel = (progressRatio - progressKnobSceneAnchorX) * progressTrack.rect.width;
            progressKnob.anchoredPosition = new Vector2(
                progressKnobScenePosition.x + travel,
                progressKnobScenePosition.y);
        }
        if (progressKnobText != null) progressKnobText.text = categoriesCompleted.ToString();
        if (moveCountText != null) moveCountText.text = fridgeHudActive ? moveCount.ToString() : $"MOVES\n{moveCount}";
        if (goldText != null) goldText.text = SaveSystem.Current.gold.ToString();
        if (levelLabel != null)
            levelLabel.text = fridgeHudActive
                ? $"Level <color=#D69535>{currentLevelNumber}</color>"
                : $"LEVEL {currentLevelNumber}";
        if (progressDotsParent != null && progressDots.Count != currentLevelRows)
            CreateProgressDots(progressDotsParent, currentLevelRows);
        for (int i = 0; i < progressDots.Count; i++)
            progressDots[i].color = i < categoriesCompleted
                ? new Color(1f, 0.55f, 0.45f, 1f)
                : new Color(1f, 1f, 1f, 0.45f);
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out RectTransform _))
            return existing.gameObject;

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Image _))
            return existing.gameObject;

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
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI authoredLabel))
        {
            authoredLabel.text = value;
            authoredLabel.alignment = alignment;
            return authoredLabel;
        }

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
        if (rect == null) return;
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            Stretch(rect);
            return;
        }

        Rect safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        min.x = Mathf.Clamp01(min.x);
        min.y = Mathf.Clamp01(min.y);
        max.x = Mathf.Clamp01(max.x);
        max.y = Mathf.Clamp01(max.y);
        if (max.x <= min.x || max.y <= min.y)
        {
            Stretch(rect);
            return;
        }

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

    private static Sprite GetFridgeBackgroundSprite()
    {
        if (fridgeBackgroundSprite == null)
            fridgeBackgroundSprite = Resources.Load<Sprite>("Art/Fridge/Background/fridge_gradient");
        return fridgeBackgroundSprite;
    }

    private static Sprite GetFridgePlainBackgroundSprite()
    {
        if (fridgePlainBackgroundSprite == null)
            fridgePlainBackgroundSprite = Resources.Load<Sprite>("Art/Fridge/Background/fridge_plain_bg");
        return fridgePlainBackgroundSprite;
    }

    private static Sprite GetFridgeShineFrameSprite()
    {
        if (fridgeShineFrameSprite == null)
            fridgeShineFrameSprite = Resources.Load<Sprite>("Art/Fridge/Background/fridge_shine_frame");
        return fridgeShineFrameSprite;
    }

    private static Sprite GetFridgeShineLinesSprite()
    {
        if (fridgeShineLinesSprite == null)
            fridgeShineLinesSprite = Resources.Load<Sprite>("Art/Fridge/Background/fridge_shine_lines");
        return fridgeShineLinesSprite;
    }

    private static Sprite GetFridgeHandleSprite()
    {
        if (fridgeHandleSprite == null)
            fridgeHandleSprite = Resources.Load<Sprite>("Art/Fridge/Background/fridge_handle");
        return fridgeHandleSprite;
    }

    private static Sprite GetFridgePaperSprite()
    {
        if (fridgePaperSprite == null)
            fridgePaperSprite = Resources.Load<Sprite>("Art/Fridge/UI/paper");
        return fridgePaperSprite;
    }

    private static Sprite GetFridgeEggSprite()
    {
        if (fridgeEggSprite == null)
            fridgeEggSprite = Resources.Load<Sprite>("Art/Fridge/Magnets/magnet_egg");
        return fridgeEggSprite;
    }

    private static Sprite GetFridgeOrangeSprite()
    {
        if (fridgeOrangeSprite == null)
            fridgeOrangeSprite = Resources.Load<Sprite>("Art/Fridge/Magnets/magnet_orange");
        return fridgeOrangeSprite;
    }

    private static Sprite GetFridgeRowSprite()
    {
        if (fridgeRowSprite == null)
            fridgeRowSprite = Resources.Load<Sprite>("Art/Fridge/Rows/magnet_holder");
        return fridgeRowSprite;
    }

    private static Sprite GetFridgeWordSprite()
    {
        if (fridgeWordSprite == null)
            fridgeWordSprite = Resources.Load<Sprite>("Art/Fridge/Magnets/magnet_word");
        return fridgeWordSprite;
    }

    private static Sprite GetFridgeProgressTrackSprite()
    {
        if (fridgeProgressTrackSprite == null)
            fridgeProgressTrackSprite = Resources.Load<Sprite>("Art/Fridge/UI/progress_dark");
        return fridgeProgressTrackSprite;
    }

    private static Sprite GetFridgeProgressFillSprite()
    {
        if (fridgeProgressFillSprite == null)
            fridgeProgressFillSprite = Resources.Load<Sprite>("Art/Fridge/UI/progress_green");
        return fridgeProgressFillSprite;
    }

    private static Sprite GetFridgeProgressOrangeSprite()
    {
        if (fridgeProgressOrangeSprite == null)
            fridgeProgressOrangeSprite = Resources.Load<Sprite>("Art/Fridge/UI/progress_orange");
        return fridgeProgressOrangeSprite;
    }

    private static Sprite GetFridgeMovesSlotSprite()
    {
        if (fridgeMovesSlotSprite == null)
            fridgeMovesSlotSprite = Resources.Load<Sprite>("Art/Fridge/UI/moves_slot");
        return fridgeMovesSlotSprite;
    }

    private static Sprite GetFridgeProgressSlotSprite()
    {
        if (fridgeProgressSlotSprite == null)
            fridgeProgressSlotSprite = Resources.Load<Sprite>("Art/Fridge/UI/progressbar_slot");
        return fridgeProgressSlotSprite;
    }

    private static Sprite GetFridgeSettingsSlotSprite()
    {
        if (fridgeSettingsSlotSprite == null)
            fridgeSettingsSlotSprite = Resources.Load<Sprite>("Art/Fridge/UI/settings_slot");
        return fridgeSettingsSlotSprite;
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
