#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameSceneAssetsSetup
{
    private const string GamePath = "Assets/_Game/Scenes/Game.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += EnsureGameSceneAssets;
    }

    [MenuItem("Tools/Word Game/Setup Game Scene Assets")]
    public static void SetupGameSceneAssets()
    {
        EnsureGameSceneAssets();
        Debug.Log("Game scene UI assets are stored in Game.unity and ready to edit.");
    }

    private static void EnsureGameSceneAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) == null) return;

        Scene gameScene = SceneManager.GetSceneByPath(GamePath);
        bool openedForSetup = !gameScene.IsValid() || !gameScene.isLoaded;
        if (openedForSetup) gameScene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);

        TableController controller = gameScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TableController>(true))
            .FirstOrDefault();
        Canvas canvas = gameScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault();

        bool changed = false;
        Transform existingHud = canvas == null ? null : canvas.transform.Find("GameHUD");
        if (existingHud != null && existingHud.Find("FlatDesignV12") == null)
        {
            Object.DestroyImmediate(existingHud.gameObject);
            existingHud = null;
            changed = true;
        }

        if (controller != null && canvas != null && existingHud == null)
        {
            BuildHud(controller, canvas);
            changed = true;
        }
        else if (controller != null && canvas != null)
            changed = EnsureGoldDisplay(controller, canvas) || changed;

        if (changed) EditorSceneManager.SaveScene(gameScene);

        if (openedForSetup) EditorSceneManager.CloseScene(gameScene, true);
        AssetDatabase.SaveAssets();
    }

    private static void BuildHud(TableController controller, Canvas canvas)
    {
        GameObject hudObject = CreateRect("GameHUD", canvas.transform);
        RectTransform hud = hudObject.GetComponent<RectTransform>();
        Stretch(hud);
        GameObject marker = CreateRect("FlatDesignV12", hud);
        marker.SetActive(false);

        CreateBubble("BubbleLeft", hud, new Vector2(-0.04f, 0.70f), new Vector2(0.14f, 0.82f));
        CreateBubble("BubbleRight", hud, new Vector2(0.88f, 0.23f), new Vector2(1.05f, 0.34f));

        GameObject topBarObject = CreateRect("TopBar", hud);
        RectTransform topBar = topBarObject.GetComponent<RectTransform>();
        SetAnchors(topBar, new Vector2(0f, 0.87f), new Vector2(1f, 0.98f));

        Image movesCard = CreateImage("MovesCard", topBar, new Color(0.20f, 0.86f, 0.72f, 1f));
        SetAnchors(movesCard.rectTransform, new Vector2(0.03f, 0.08f), new Vector2(0.22f, 0.92f));
        AddShadow(movesCard.gameObject);
        TextMeshProUGUI moves = CreateText("MoveCount", movesCard.transform, "MOVES\n0", TextAlignmentOptions.Center, 40f,
            new Color(0.06f, 0.12f, 0.30f, 1f));
        Stretch(moves.rectTransform);

        Image progressCard = CreateImage("ProgressCard", topBar, new Color(0.98f, 0.97f, 0.93f, 1f));
        SetAnchors(progressCard.rectTransform, new Vector2(0.24f, 0.08f), new Vector2(0.80f, 0.92f));
        AddShadow(progressCard.gameObject);
        TextMeshProUGUI progress = CreateText("Progress", progressCard.transform, "0 COMPLETE • 6 TOTAL",
            TextAlignmentOptions.Center, 25f, new Color(0.08f, 0.12f, 0.30f, 1f));
        SetAnchors(progress.rectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.72f, 0.96f));

        Image progressTrack = CreateImage("ProgressTrack", progressCard.transform, new Color(0.08f, 0.13f, 0.32f, 1f));
        SetAnchors(progressTrack.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.47f));
        Image progressFill = CreateImage("Fill", progressTrack.transform, new Color(0.18f, 0.84f, 0.72f, 1f));
        Stretch(progressFill.rectTransform);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
        Image progressKnob = CreateImage("Knob", progressTrack.transform, new Color(0.22f, 0.55f, 1f, 1f));
        progressKnob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        progressKnob.type = Image.Type.Simple;
        progressKnob.rectTransform.anchorMin = progressKnob.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        progressKnob.rectTransform.sizeDelta = new Vector2(58f, 58f);
        progressKnob.rectTransform.anchoredPosition = Vector2.zero;
        TextMeshProUGUI knobValue = CreateText("Value", progressKnob.transform, "0", TextAlignmentOptions.Center, 25f, Color.white);
        Stretch(knobValue.rectTransform);

        TextMeshProUGUI gold = CreateGoldDisplay(progressCard.transform);

        Image settings = CreateButton("SettingsButton", topBar, new Color(1f, 0.38f, 0.42f, 1f),
            new Vector2(0.82f, 0.08f), new Vector2(0.97f, 0.92f));
        TextMeshProUGUI settingsLabel = CreateText("Label", settings.transform, "SET", TextAlignmentOptions.Center, 28f, Color.white);
        Stretch(settingsLabel.rectTransform);

        Image ribbonLeft = CreateImage("RibbonLeft", hud, new Color(0.52f, 0.10f, 0.68f, 1f));
        SetAnchors(ribbonLeft.rectTransform, new Vector2(0.29f, 0.817f), new Vector2(0.39f, 0.847f));
        Image ribbonRight = CreateImage("RibbonRight", hud, new Color(0.52f, 0.10f, 0.68f, 1f));
        SetAnchors(ribbonRight.rectTransform, new Vector2(0.61f, 0.817f), new Vector2(0.71f, 0.847f));
        Image levelRibbon = CreateImage("LevelRibbon", hud, new Color(0.65f, 0.18f, 0.82f, 1f));
        SetAnchors(levelRibbon.rectTransform, new Vector2(0.34f, 0.81f), new Vector2(0.66f, 0.855f));
        AddShadow(levelRibbon.gameObject);
        TextMeshProUGUI level = CreateText("LevelLabel", levelRibbon.transform, "LEVEL 1", TextAlignmentOptions.Center,
            28f, Color.white);
        Stretch(level.rectTransform);

        GameObject boosterObject = CreateRect("BoosterArea", hud);
        RectTransform booster = boosterObject.GetComponent<RectTransform>();
        SetAnchors(booster, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.12f));

        Image hint = CreateButton("HintButton", booster, new Color(0.46f, 0.36f, 0.78f, 1f),
            new Vector2(0.43f, 0.24f), new Vector2(0.57f, 0.92f));
        CreateBulbIcon(hint.transform);

        Image badge = CreateImage("CountBadge", hint.transform, new Color(1f, 0.48f, 0.48f, 1f));
        SetAnchors(badge.rectTransform, new Vector2(0.66f, 0.64f), new Vector2(1.14f, 1.12f));
        TextMeshProUGUI count = CreateText("Count", badge.transform, "3", TextAlignmentOptions.Center, 30f, Color.white);
        Stretch(count.rectTransform);

        TextMeshProUGUI hintLabel = CreateText("HintLabel", booster, "HINT", TextAlignmentOptions.Center, 22f,
            new Color(0.28f, 0.20f, 0.48f, 1f));
        SetAnchors(hintLabel.rectTransform, new Vector2(0.43f, 0f), new Vector2(0.57f, 0.23f));

        CreateResultScreens(hud, out GameObject winScreen, out GameObject loseScreen,
            out TextMeshProUGUI winGold, out TextMeshProUGUI reward,
            out Button doubleReward, out Button continueButton, out Button retryButton,
            out Button watchAdContinue, out Button spendGoldContinue, out Button viewBoard,
            out TextMeshProUGUI loseExplanation);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("gameHud").objectReferenceValue = hud;
        serializedController.FindProperty("progressText").objectReferenceValue = progress;
        serializedController.FindProperty("progressFill").objectReferenceValue = progressFill;
        serializedController.FindProperty("progressKnob").objectReferenceValue = progressKnob.rectTransform;
        serializedController.FindProperty("progressKnobText").objectReferenceValue = knobValue;
        serializedController.FindProperty("moveCountText").objectReferenceValue = moves;
        serializedController.FindProperty("goldText").objectReferenceValue = gold;
        serializedController.FindProperty("levelLabel").objectReferenceValue = level;
        serializedController.FindProperty("hintCountText").objectReferenceValue = count;
        serializedController.FindProperty("progressDotsParent").objectReferenceValue = null;
        serializedController.FindProperty("winScreen").objectReferenceValue = winScreen;
        serializedController.FindProperty("loseScreen").objectReferenceValue = loseScreen;
        serializedController.FindProperty("winGoldText").objectReferenceValue = winGold;
        serializedController.FindProperty("rewardText").objectReferenceValue = reward;
        serializedController.FindProperty("doubleRewardButton").objectReferenceValue = doubleReward;
        serializedController.FindProperty("continueButton").objectReferenceValue = continueButton;
        serializedController.FindProperty("retryButton").objectReferenceValue = retryButton;
        serializedController.FindProperty("watchAdContinueButton").objectReferenceValue = watchAdContinue;
        serializedController.FindProperty("spendGoldContinueButton").objectReferenceValue = spendGoldContinue;
        serializedController.FindProperty("viewBoardButton").objectReferenceValue = viewBoard;
        serializedController.FindProperty("loseExplanationText").objectReferenceValue = loseExplanation;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    private static void CreateResultScreens(RectTransform hud, out GameObject winScreen, out GameObject loseScreen,
        out TextMeshProUGUI winGold, out TextMeshProUGUI reward, out Button doubleReward,
        out Button continueButton, out Button retryButton, out Button watchAdContinue,
        out Button spendGoldContinue, out Button viewBoard, out TextMeshProUGUI loseExplanation)
    {
        Image winBackground = CreateImage("WinScreen", hud, new Color(0.10f, 0.08f, 0.24f, 0.72f));
        Stretch(winBackground.rectTransform);
        Image winPanel = CreateImage("Panel", winBackground.transform, new Color(0.97f, 0.95f, 1f, 1f));
        SetAnchors(winPanel.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        AddShadow(winPanel.gameObject);

        Image gold = CreateImage("Gold", winPanel.transform, new Color(1f, 0.82f, 0.28f, 1f));
        SetAnchors(gold.rectTransform, new Vector2(0.05f, 0.90f), new Vector2(0.30f, 0.97f));
        TextMeshProUGUI goldLabel = CreateText("Label", gold.transform, "GOLD", TextAlignmentOptions.MidlineLeft, 24f,
            new Color(0.10f, 0.10f, 0.25f, 1f));
        SetAnchors(goldLabel.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.50f, 1f));
        winGold = CreateText("Value", gold.transform, "500", TextAlignmentOptions.MidlineRight, 26f,
            new Color(0.10f, 0.10f, 0.25f, 1f));
        SetAnchors(winGold.rectTransform, new Vector2(0.48f, 0f), new Vector2(0.92f, 1f));

        TextMeshProUGUI success = CreateText("SuccessText", winPanel.transform, "LEVEL COMPLETE!",
            TextAlignmentOptions.Center, 58f, new Color(0.30f, 0.18f, 0.66f, 1f));
        SetAnchors(success.rectTransform, new Vector2(0.10f, 0.68f), new Vector2(0.90f, 0.84f));

        Image rewardPanel = CreateImage("Reward", winPanel.transform, new Color(0.84f, 0.96f, 0.88f, 1f));
        SetAnchors(rewardPanel.rectTransform, new Vector2(0.30f, 0.49f), new Vector2(0.70f, 0.63f));
        AddShadow(rewardPanel.gameObject);
        TextMeshProUGUI rewardLabel = CreateText("Label", rewardPanel.transform, "REWARD",
            TextAlignmentOptions.Center, 22f, new Color(0.10f, 0.16f, 0.28f, 1f));
        SetAnchors(rewardLabel.rectTransform, new Vector2(0f, 0.56f), new Vector2(1f, 0.96f));
        reward = CreateText("Value", rewardPanel.transform, "+40 GOLD", TextAlignmentOptions.Center, 35f,
            new Color(0.12f, 0.48f, 0.30f, 1f));
        SetAnchors(reward.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.60f));

        Image doubleImage = CreateButton("DoubleRewardButton", winPanel.transform, new Color(0.36f, 0.25f, 0.72f, 1f),
            new Vector2(0.20f, 0.22f), new Vector2(0.80f, 0.33f));
        doubleReward = doubleImage.GetComponent<Button>();
        TextMeshProUGUI doubleLabel = CreateText("Label", doubleImage.transform, "WATCH AD  •  X2 REWARDS",
            TextAlignmentOptions.Center, 28f, Color.white);
        Stretch(doubleLabel.rectTransform);

        Image continueImage = CreateButton("ContinueButton", winPanel.transform, new Color(0.20f, 0.78f, 0.62f, 1f),
            new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.19f));
        continueButton = continueImage.GetComponent<Button>();
        TextMeshProUGUI continueLabel = CreateText("Label", continueImage.transform, "CONTINUE",
            TextAlignmentOptions.Center, 32f, Color.white);
        Stretch(continueLabel.rectTransform);

        Image loseBackground = CreateImage("LoseScreen", hud, new Color(0.10f, 0.08f, 0.24f, 0.62f));
        Stretch(loseBackground.rectTransform);
        Image losePanel = CreateImage("Panel", loseBackground.transform, new Color(1f, 0.95f, 0.96f, 1f));
        SetAnchors(losePanel.rectTransform, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.72f));
        AddShadow(losePanel.gameObject);
        TextMeshProUGUI popupLabel = CreateText("PopupLabel", losePanel.transform, "LOSE POPUP",
            TextAlignmentOptions.Center, 25f, new Color(0.20f, 0.18f, 0.34f, 1f));
        SetAnchors(popupLabel.rectTransform, new Vector2(0.15f, 0.88f), new Vector2(0.85f, 0.98f));
        TextMeshProUGUI loseTitle = CreateText("Title", losePanel.transform, "YOU LOST",
            TextAlignmentOptions.Center, 48f, new Color(0.90f, 0.24f, 0.30f, 1f));
        SetAnchors(loseTitle.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.87f));
        loseExplanation = CreateText("Explanation", losePanel.transform, "YOU RAN OUT OF MOVES",
            TextAlignmentOptions.Center, 25f, new Color(0.20f, 0.18f, 0.34f, 1f));
        SetAnchors(loseExplanation.rectTransform, new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.67f));

        Image watchImage = CreateButton("WatchAdContinueButton", losePanel.transform,
            new Color(0.36f, 0.25f, 0.72f, 1f), new Vector2(0.08f, 0.24f), new Vector2(0.46f, 0.49f));
        watchAdContinue = watchImage.GetComponent<Button>();
        TextMeshProUGUI watchLabel = CreateText("Label", watchImage.transform, "WATCH AD\nCONTINUE",
            TextAlignmentOptions.Center, 25f, Color.white);
        Stretch(watchLabel.rectTransform);

        Image spendImage = CreateButton("SpendGoldContinueButton", losePanel.transform,
            new Color(1f, 0.70f, 0.20f, 1f), new Vector2(0.54f, 0.24f), new Vector2(0.92f, 0.49f));
        spendGoldContinue = spendImage.GetComponent<Button>();
        TextMeshProUGUI spendLabel = CreateText("Label", spendImage.transform, "SPEND 900 GOLD\nCONTINUE",
            TextAlignmentOptions.Center, 23f, new Color(0.14f, 0.12f, 0.25f, 1f));
        Stretch(spendLabel.rectTransform);

        Image retryImage = CreateButton("RetryButton", losePanel.transform, new Color(1f, 0.50f, 0.34f, 1f),
            new Vector2(0.20f, 0.06f), new Vector2(0.80f, 0.18f));
        retryButton = retryImage.GetComponent<Button>();
        TextMeshProUGUI retryLabel = CreateText("Label", retryImage.transform, "LET ME FAIL",
            TextAlignmentOptions.Center, 27f, Color.white);
        Stretch(retryLabel.rectTransform);

        Image viewImage = CreateButton("ViewBoardButton", loseBackground.transform, new Color(1f, 1f, 1f, 0.92f),
            new Vector2(0.23f, 0.19f), new Vector2(0.77f, 0.26f));
        viewBoard = viewImage.GetComponent<Button>();
        TextMeshProUGUI viewLabel = CreateText("Label", viewImage.transform, "TAP HERE TO VIEW GAME BOARD",
            TextAlignmentOptions.Center, 23f, new Color(0.18f, 0.16f, 0.30f, 1f));
        Stretch(viewLabel.rectTransform);

        winScreen = winBackground.gameObject;
        loseScreen = loseBackground.gameObject;
        winScreen.SetActive(false);
        loseScreen.SetActive(false);
    }

    private static bool EnsureGoldDisplay(TableController controller, Canvas canvas)
    {
        Transform topBar = canvas.transform.Find("GameHUD/TopBar");
        if (topBar == null) return false;

        Transform progressCard = topBar.Find("ProgressCard");
        if (progressCard != null)
        {
            SerializedObject flatController = new SerializedObject(controller);
            flatController.FindProperty("progressText").objectReferenceValue = progressCard.Find("Progress")?.GetComponent<TextMeshProUGUI>();
            flatController.FindProperty("progressFill").objectReferenceValue = progressCard.Find("ProgressTrack/Fill")?.GetComponent<Image>();
            flatController.FindProperty("progressKnob").objectReferenceValue = progressCard.Find("ProgressTrack/Knob") as RectTransform;
            flatController.FindProperty("progressKnobText").objectReferenceValue = progressCard.Find("ProgressTrack/Knob/Value")?.GetComponent<TextMeshProUGUI>();
            flatController.FindProperty("moveCountText").objectReferenceValue = topBar.Find("MovesCard/MoveCount")?.GetComponent<TextMeshProUGUI>();
            flatController.FindProperty("goldText").objectReferenceValue = progressCard.Find("GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
            flatController.FindProperty("levelLabel").objectReferenceValue = canvas.transform.Find("GameHUD/LevelRibbon/LevelLabel")?.GetComponent<TextMeshProUGUI>();
            flatController.ApplyModifiedPropertiesWithoutUndo();
            return false;
        }

        TextMeshProUGUI gold = topBar.Find("GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
        bool created = gold == null;
        if (created) gold = CreateGoldDisplay(topBar);

        TextMeshProUGUI moves = topBar.Find("MoveCount")?.GetComponent<TextMeshProUGUI>();
        if (moves != null)
            SetAnchors(moves.rectTransform, new Vector2(0.27f, 0f), new Vector2(0.57f, 1f));

        TextMeshProUGUI progress = topBar.Find("Progress")?.GetComponent<TextMeshProUGUI>();
        if (progress != null)
            SetAnchors(progress.rectTransform, new Vector2(0.11f, 0f), new Vector2(0.29f, 1f));

        RectTransform goldDisplay = topBar.Find("GoldDisplay") as RectTransform;
        if (goldDisplay != null)
        {
            SetAnchors(goldDisplay, new Vector2(0.56f, 0.12f), new Vector2(0.81f, 0.88f));
            RectTransform coin = goldDisplay.Find("Coin") as RectTransform;
            if (coin != null) SetAnchors(coin, new Vector2(0.02f, 0.23f), new Vector2(0.23f, 0.77f));
            if (gold != null) SetAnchors(gold.rectTransform, new Vector2(0.27f, 0f), Vector2.one);
        }

        RectTransform settings = topBar.Find("SettingsButton") as RectTransform;
        if (settings != null)
            SetAnchors(settings, new Vector2(0.83f, 0.12f), new Vector2(0.98f, 0.88f));

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("goldText").objectReferenceValue = gold;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        return true;
    }

    private static TextMeshProUGUI CreateGoldDisplay(Transform topBar)
    {
        GameObject display = CreateRect("GoldDisplay", topBar);
        SetAnchors(display.GetComponent<RectTransform>(), new Vector2(0.70f, 0.53f), new Vector2(0.97f, 0.96f));

        Image coin = CreateImage("Coin", display.transform, new Color(1f, 0.78f, 0.24f, 1f));
        coin.raycastTarget = false;
        SetAnchors(coin.rectTransform, new Vector2(0.02f, 0.23f), new Vector2(0.23f, 0.77f));

        TextMeshProUGUI gold = CreateText("GoldText", display.transform, "500", TextAlignmentOptions.MidlineLeft,
            32f, new Color(1f, 0.86f, 0.64f));
        SetAnchors(gold.rectTransform, new Vector2(0.27f, 0f), Vector2.one);
        return gold;
    }

    private static void CreateCategoryIcon(Transform parent)
    {
        Color[] colors =
        {
            new Color(1f, 0.55f, 0.48f),
            new Color(1f, 0.82f, 0.42f),
            new Color(0.53f, 0.88f, 0.78f)
        };

        for (int i = 0; i < colors.Length; i++)
        {
            Image layer = CreateImage($"CategoryLayer{i + 1}", parent, colors[i]);
            float y = 0.31f + i * 0.13f;
            SetAnchors(layer.rectTransform, new Vector2(0.045f, y), new Vector2(0.105f, y + 0.23f));
        }
    }

    private static void CreateBulbIcon(Transform parent)
    {
        Image head = CreateImage("BulbHead", parent, new Color(1f, 0.92f, 0.48f, 1f));
        head.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        head.type = Image.Type.Simple;
        SetAnchors(head.rectTransform, new Vector2(0.31f, 0.39f), new Vector2(0.69f, 0.78f));
        Image baseImage = CreateImage("BulbBase", parent, new Color(1f, 0.92f, 0.48f, 1f));
        SetAnchors(baseImage.rectTransform, new Vector2(0.40f, 0.22f), new Vector2(0.60f, 0.44f));
    }

    private static void CreateGearIcon(Transform parent)
    {
        Color white = Color.white;
        Image vertical = CreateImage("GearVertical", parent, white);
        SetAnchors(vertical.rectTransform, new Vector2(0.44f, 0.14f), new Vector2(0.56f, 0.86f));
        Image horizontal = CreateImage("GearHorizontal", parent, white);
        SetAnchors(horizontal.rectTransform, new Vector2(0.14f, 0.44f), new Vector2(0.86f, 0.56f));
        Image diagonalA = CreateImage("GearDiagonalA", parent, white);
        SetAnchors(diagonalA.rectTransform, new Vector2(0.44f, 0.14f), new Vector2(0.56f, 0.86f));
        diagonalA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image diagonalB = CreateImage("GearDiagonalB", parent, white);
        SetAnchors(diagonalB.rectTransform, new Vector2(0.44f, 0.14f), new Vector2(0.56f, 0.86f));
        diagonalB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        Image center = CreateImage("GearCenter", parent, white);
        center.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        center.type = Image.Type.Simple;
        SetAnchors(center.rectTransform, new Vector2(0.27f, 0.27f), new Vector2(0.73f, 0.73f));
        Image hole = CreateImage("GearHole", center.transform, new Color(1f, 0.38f, 0.42f, 1f));
        hole.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        hole.type = Image.Type.Simple;
        SetAnchors(hole.rectTransform, new Vector2(0.32f, 0.32f), new Vector2(0.68f, 0.68f));
    }

    private static void CreateBubble(string objectName, Transform parent, Vector2 min, Vector2 max)
    {
        Image bubble = CreateImage(objectName, parent, new Color(1f, 1f, 1f, 0.08f));
        bubble.raycastTarget = false;
        SetAnchors(bubble.rectTransform, min, max);
        bubble.transform.SetAsFirstSibling();
    }

    private static Image CreateButton(string objectName, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        Image image = CreateImage(objectName, parent, color);
        image.gameObject.AddComponent<Button>();
        SetAnchors(image.rectTransform, min, max);
        AddShadow(image.gameObject);
        return image;
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.transform.SetParent(parent, false);
        Image image = result.GetComponent<Image>();
        image.color = color;
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (rounded != null)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value,
        TextAlignmentOptions alignment, float fontSize, Color color)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        result.transform.SetParent(parent, false);
        TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (displayFont != null) text.font = displayFont;
        text.text = value;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = fontSize;
        text.outlineColor = new Color(0.05f, 0.07f, 0.20f, 0.70f);
        text.outlineWidth = 0.10f;
        return text;
    }

    private static void AddShadow(GameObject target)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.08f, 0.22f, 0.62f);
        shadow.effectDistance = new Vector2(0f, -9f);
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = new Color(0.06f, 0.08f, 0.22f, 0.88f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
