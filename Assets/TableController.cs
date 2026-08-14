using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI moveCountText;
    private TextMeshProUGUI levelLabel;
    private readonly List<Image> progressDots = new List<Image>();
    private Transform progressDotsParent;
    private WordButton selectedWord;
    private int remainingMoves;
    private bool gameEnded;
    private int categoriesCompleted;
    private int moveCount;
    private static Sprite roundedPanelSprite;
    private static Sprite roundedButtonSprite;
    private static Sprite bulbIconSprite;
    private static Sprite gearIconSprite;
    private static Sprite lockIconSprite;
    private static readonly Color PastelButtonColor = new Color(1.00f, 0.82f, 0.66f, 1f);
    private WordIconLibrary iconLibrary;
    private LevelDatabase levelDatabase;
    private int currentLevelNumber = 1;
    private int activeLevelIndex;
    private PlayerSaveData playerSaveData;

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

        levelDatabase = Resources.Load<LevelDatabase>("LevelDatabase");
        iconLibrary = Resources.Load<WordIconLibrary>("MainWordIconLibrary");
        activeLevelIndex = levelDatabase == null || levelDatabase.levels.Count == 0
            ? 0
            : Mathf.Clamp(levelIndex, 0, levelDatabase.levels.Count - 1);
        LevelData level = levelDatabase == null ? null : levelDatabase.GetLevel(activeLevelIndex);
        if (level == null)
        {
            Debug.LogError("LevelDatabase is missing or contains no levels. Use Tools > Word Game > Create Example Levels.", this);
            return;
        }

        if (!level.IsValid(out string validationError))
        {
            Debug.LogError($"Level {level.levelNumber} is invalid: {validationError}", level);
            return;
        }

        currentLevelRows = level.categories.Count;
        currentLevelNumber = level.levelNumber;
        categoriesCompleted = 0;
        remainingMoves = level.moveCount;
        moveCount = remainingMoves;
        gameEnded = false;
        selectedWord = null;
        Canvas canvas = wordContainer.GetComponentInParent<Canvas>();
        Transform previousResult = canvas == null ? null : canvas.transform.Find("ResultOverlay");
        if (previousResult != null) Destroy(previousResult.gameObject);
        ClearBoard();
        ApplyBoardStyle();

        List<Category> selectedCategories = level.categories;

        List<BoardWord> shuffledWords = selectedCategories
            .SelectMany(category => category.words.Select(word => new BoardWord
            {
                word = word,
                categoryId = category.id
            }))
            .OrderBy(_ => Random.value)
            .ToList();

        const int columns = 4;
        for (int rowIndex = 0; rowIndex < currentLevelRows; rowIndex++)
        {
            GameObject newRow = Instantiate(rowPrefab, wordContainer);
            newRow.name = $"Row {rowIndex + 1}";
            Image rowBackground = newRow.GetComponent<Image>();
            if (rowBackground != null) rowBackground.color = Color.clear;

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
                    AddShadow(newButton, new Color(0.20f, 0.12f, 0.35f, 0.16f), new Vector2(0f, -5f));
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
        SwapWords(first, word);
    }

    private void SwapWords(WordButton first, WordButton second)
    {
        Transform firstRow = first.transform.parent;
        Transform secondRow = second.transform.parent;
        int firstIndex = first.transform.GetSiblingIndex();
        int secondIndex = second.transform.GetSiblingIndex();
        bool changedRows = firstRow != secondRow;

        if (changedRows)
        {
            first.transform.SetParent(secondRow, false);
            second.transform.SetParent(firstRow, false);
            first.transform.SetSiblingIndex(secondIndex);
            second.transform.SetSiblingIndex(firstIndex);
            remainingMoves = Mathf.Max(0, remainingMoves - 1);
            moveCount = remainingMoves;
        }
        else
        {
            first.transform.SetSiblingIndex(secondIndex);
            second.transform.SetSiblingIndex(firstIndex);
        }

        CheckRow(firstRow);
        if (secondRow != firstRow) CheckRow(secondRow);
        RefreshIndicators();

        if (categoriesCompleted >= currentLevelRows)
            EndGame(true);
        else if (remainingMoves <= 0)
            EndGame(false);
    }

    private void CheckRow(Transform row)
    {
        WordButton[] words = row.GetComponentsInChildren<WordButton>();
        if (words.Length != 4 || words.Any(word => word.IsMatched)) return;

        string categoryId = words[0].CategoryId;
        if (words.All(word => word.CategoryId == categoryId))
        {
            foreach (WordButton word in words) word.SetMatched(true);
            categoriesCompleted++;
        }
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
        }

        Canvas canvas = wordContainer.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform oldResult = canvas.transform.Find("ResultOverlay");
        if (oldResult != null) Destroy(oldResult.gameObject);

        GameObject overlay = CreatePanel("ResultOverlay", canvas.transform, new Color(0.18f, 0.12f, 0.32f, 0.88f));
        SetAnchors(overlay.GetComponent<RectTransform>(), new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.64f));
        bool hasNextLevel = won && levelDatabase != null && activeLevelIndex + 1 < levelDatabase.levels.Count;
        string resultMessage = won
            ? (hasNextLevel ? "LEVEL COMPLETE!" : "ALL LEVELS COMPLETE!")
            : "OUT OF MOVES";
        TextMeshProUGUI result = CreateLabel("Result", overlay.transform,
            resultMessage, TextAlignmentOptions.Center);
        result.fontSize = 52;
        result.color = won ? new Color(0.70f, 1f, 0.78f) : new Color(1f, 0.72f, 0.72f);
        Stretch(result.rectTransform);
        overlay.transform.SetAsLastSibling();

        if (hasNextLevel)
            StartCoroutine(LoadNextLevelAfterDelay(1.5f));
    }

    private IEnumerator LoadNextLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadLevel(activeLevelIndex + 1);
    }

    private void ApplyBoardStyle()
    {
        Image boardBackground = wordContainer.GetComponent<Image>();
        if (boardBackground != null)
        {
            boardBackground.sprite = GetRoundedPanelSprite();
            boardBackground.type = Image.Type.Sliced;
            boardBackground.color = new Color(0.87f, 0.83f, 0.97f, 0.95f);
            AddShadow(wordContainer.gameObject, new Color(0.20f, 0.12f, 0.35f, 0.22f), new Vector2(0f, -8f));
        }

        Transform backgroundTransform = wordContainer.parent.Find("background");
        Image backgroundImage = backgroundTransform == null ? null : backgroundTransform.GetComponent<Image>();
        Texture2D pastelTexture = Resources.Load<Texture2D>("PastelBackground");
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
        boardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 960f);
        float targetBoardHeight = GetBoardHeightForRows(rowCount);
        float boardCenterY = rowCount <= 4 ? 5f : rowCount == 5 ? -45f : -100f;
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
            case 1: return 360f;
            case 2: return 520f;
            case 3: return 700f;
            case 4: return 900f;
            case 5: return 1120f;
            case 6: return 1360f;
            default: return 1360f + (rowCount - 6) * 150f;
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

        Transform previousHud = canvas.transform.Find("GameHUD");
        if (previousHud != null) DestroyGeneratedObject(previousHud.gameObject);

        GameObject hud = CreateRect("GameHUD", canvas.transform);
        ApplySafeArea(hud.GetComponent<RectTransform>());

        CreateDecorativeBubble(hud.transform, "BubbleLeft", new Vector2(-0.04f, 0.70f), new Vector2(0.14f, 0.82f), new Color(1f, 1f, 1f, 0.08f));
        CreateDecorativeBubble(hud.transform, "BubbleRight", new Vector2(0.88f, 0.23f), new Vector2(1.05f, 0.34f), new Color(1f, 1f, 1f, 0.07f));

        GameObject topBar = CreatePanel("TopBar", hud.transform, new Color(0.36f, 0.25f, 0.62f, 0.94f));
        SetAnchors(topBar.GetComponent<RectTransform>(), new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.97f));
        AddShadow(topBar, new Color(0.16f, 0.10f, 0.30f, 0.24f), new Vector2(0f, -7f));

        CreateCategoryIcon(topBar.transform);

        progressText = CreateLabel("Progress", topBar.transform, "0/0", TextAlignmentOptions.MidlineLeft);
        SetAnchors(progressText.rectTransform, new Vector2(0.12f, 0f), new Vector2(0.40f, 1f));

        moveCountText = CreateLabel("MoveCount", topBar.transform, "Moves: 0", TextAlignmentOptions.Center);
        SetAnchors(moveCountText.rectTransform, new Vector2(0.38f, 0f), new Vector2(0.78f, 1f));

        CreateHudButton("SettingsButton", topBar.transform, "", new Vector2(0.82f, 0.12f), new Vector2(0.97f, 0.88f));

        levelLabel = CreateLabel("LevelLabel", hud.transform, "LEVEL 1", TextAlignmentOptions.Center);
        levelLabel.fontSize = 28;
        levelLabel.color = new Color(0.28f, 0.20f, 0.48f, 1f);
        SetAnchors(levelLabel.rectTransform, new Vector2(0.35f, 0.845f), new Vector2(0.65f, 0.885f));
        progressDotsParent = hud.transform;
        CreateProgressDots(progressDotsParent, 1);

        GameObject boosterArea = CreatePanel("BoosterArea", hud.transform, Color.clear);
        SetAnchors(boosterArea.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.12f));
        string savedHintCount = (playerSaveData ?? SaveSystem.Current).hintCount.ToString();
        CreateHudButton("HintButton", boosterArea.transform, "", new Vector2(0.43f, 0.24f), new Vector2(0.57f, 0.92f), savedHintCount);

        TextMeshProUGUI hintLabel = CreateLabel("HintLabel", boosterArea.transform, "HINT", TextAlignmentOptions.Center);
        hintLabel.fontSize = 22;
        hintLabel.color = new Color(0.28f, 0.20f, 0.48f, 1f);
        SetAnchors(hintLabel.rectTransform, new Vector2(0.43f, 0f), new Vector2(0.57f, 0.23f));

        hud.transform.SetAsLastSibling();
    }

    private void RefreshIndicators()
    {
        if (progressText != null) progressText.text = $"{categoriesCompleted}/{currentLevelRows}";
        if (moveCountText != null) moveCountText.text = $"Moves: {moveCount}";
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
