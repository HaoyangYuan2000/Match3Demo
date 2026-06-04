using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    public int width = 7;
    public int height = 9;

    [Header("Gem Colors (fallback when no prefab)")]
    public Color[] gemColors = new Color[]
    {
        new Color(0.95f, 0.3f,  0.3f),
        new Color(0.3f,  0.65f, 0.95f),
        new Color(0.35f, 0.80f, 0.40f),
        new Color(0.95f, 0.80f, 0.2f),
        new Color(0.75f, 0.4f,  0.90f),
        new Color(0.95f, 0.60f, 0.2f),
    };

    [Header("Gem Prefabs (VFX Kit Chip_1)")]
    public GameObject[] gemPrefabs = new GameObject[6];
    // [0] Red    → Chip_1/Red
    // [1] Blue   → Chip_1/Blue_Root
    // [2] Green  → Chip_1/Green
    // [3] Yellow → Chip_1/Orange
    // [4] Purple → Chip_1/Violet
    // [5] Orange → Chip_1/Pink

    [Header("Game Rules")]
    public int maxMoves = 25;
    public int targetScore = 2000;

    [Header("UI")]
    public Text scoreText;

    // World-space cell size (1 unit per cell)
    private const float CELL = 1.0f;
    // Shift board down so header has room
    private const float BOARD_OFFSET_Y = -0.5f;

    private int[,] board;
    private GameObject[,] gems;
    private int selectedX = -1, selectedY = -1;
    private bool isProcessing = false;
    private bool isGameOver = false;
    private int score = 0;
    private int movesLeft;
    private Canvas canvas;
    private RectTransform canvasRT;
    private Camera gameCamera;
    private Text movesText;
    private Text resultText;
    private Text finalScoreValueText;
    private GameObject resultPanel;
    private Text shuffleNoticeText;
    private float idleTime = 0f;
    private Coroutine hintCoroutine;
    private int hintX1, hintY1, hintX2, hintY2;

    // ── Lifecycle ──────────────────────────────────────────────
    void Start()
    {
        movesLeft = maxMoves;
        SetupUI();
        InitBoard();
        DrawBoard();
    }

    void Update()
    {
        if (isGameOver) return;

        // Hint timer (only when not processing and nothing selected)
        if (!isProcessing && selectedX == -1)
        {
            idleTime += Time.deltaTime;
            if (idleTime >= 5f && hintCoroutine == null) ShowHint();
        }

        // Mouse click → gem selection (ignore UI clicks)
        if (ClickThisFrame() && !isProcessing)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 wp = gameCamera.ScreenToWorldPoint((Vector3)CurrentMousePosition());
            wp.z = 0f;
            int gx = Mathf.RoundToInt(wp.x / CELL + (width  - 1) * 0.5f);
            int gy = Mathf.RoundToInt((height - 1) * 0.5f + BOARD_OFFSET_Y / CELL - wp.y / CELL);
            if (gx >= 0 && gx < width && gy >= 0 && gy < height)
                OnGemClick(gx, gy);
        }
    }

    // ── UI Setup ───────────────────────────────────────────────
    void SetupUI()
    {
        // Camera
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            gameCamera = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0, 0, -10);
        }
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.08f, 0.05f, 0.18f);
        gameCamera.orthographic = true;
        gameCamera.depth = -1;
        // Fit board to screen: take whichever constraint (width or height) requires more zoom-out
        float aspect = (float)Screen.width / Screen.height;
        float sizeForHeight = (height * CELL) / (2f * 0.65f);
        float sizeForWidth  = (width  * CELL) / (2f * aspect * 0.90f);
        gameCamera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // Canvas (ScreenSpaceCamera, renders on top of world gems)
        GameObject canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = gameCamera;
        canvas.planeDistance = 100f;
        canvas.sortingOrder = 100;
        canvasRT = canvasGO.GetComponent<RectTransform>();
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Header bar
        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(canvasGO.transform, false);
        Image headerImg = headerGO.AddComponent<Image>();
        headerImg.color = new Color(0.12f, 0.08f, 0.26f);
        RectTransform hRT = headerGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = Vector2.zero; hRT.sizeDelta = new Vector2(0f, 130f);

        // Stat cards
        Text gl, gv, scl, ml;
        MakeStatCard(canvasGO.transform, "GOAL", targetScore.ToString(),
            new Color(0.90f, 0.55f, 0.10f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -15f), new Vector2(170f, 100f), out gl, out gv);
        MakeStatCard(canvasGO.transform, "SCORE", "0",
            new Color(0.25f, 0.55f, 0.90f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -15f), new Vector2(170f, 100f), out scl, out scoreText);
        MakeStatCard(canvasGO.transform, "MOVES", maxMoves.ToString(),
            new Color(0.25f, 0.70f, 0.35f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -15f), new Vector2(170f, 100f), out ml, out movesText);

        // Result panel
        resultPanel = new GameObject("ResultPanel");
        resultPanel.transform.SetParent(canvasGO.transform, false);
        Image panelImg = resultPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.82f);
        RectTransform rpRT = resultPanel.GetComponent<RectTransform>();
        rpRT.anchorMin = Vector2.zero; rpRT.anchorMax = Vector2.one;
        rpRT.offsetMin = rpRT.offsetMax = Vector2.zero;

        GameObject resultTextGO = new GameObject("ResultText");
        resultTextGO.transform.SetParent(resultPanel.transform, false);
        resultText = resultTextGO.AddComponent<Text>();
        resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultText.fontSize = 90; resultText.fontStyle = FontStyle.Bold;
        resultText.alignment = TextAnchor.MiddleCenter;
        RectTransform rtRT = resultTextGO.GetComponent<RectTransform>();
        rtRT.anchorMin = new Vector2(0f, 0.5f); rtRT.anchorMax = new Vector2(1f, 0.5f);
        rtRT.anchoredPosition = new Vector2(0f, 90f); rtRT.sizeDelta = new Vector2(0f, 120f);

        GameObject finalLabelGO = new GameObject("FinalScoreLabel");
        finalLabelGO.transform.SetParent(resultPanel.transform, false);
        Text finalLabel = finalLabelGO.AddComponent<Text>();
        finalLabel.text = "FINAL SCORE";
        finalLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finalLabel.fontSize = 28; finalLabel.alignment = TextAnchor.MiddleCenter;
        finalLabel.color = new Color(0.60f, 0.60f, 0.60f);
        RectTransform flRT = finalLabelGO.GetComponent<RectTransform>();
        flRT.anchorMin = new Vector2(0f, 0.5f); flRT.anchorMax = new Vector2(1f, 0.5f);
        flRT.anchoredPosition = new Vector2(0f, 10f); flRT.sizeDelta = new Vector2(0f, 40f);

        GameObject finalValueGO = new GameObject("FinalScoreValue");
        finalValueGO.transform.SetParent(resultPanel.transform, false);
        finalScoreValueText = finalValueGO.AddComponent<Text>();
        finalScoreValueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finalScoreValueText.fontSize = 64; finalScoreValueText.fontStyle = FontStyle.Bold;
        finalScoreValueText.alignment = TextAnchor.MiddleCenter;
        finalScoreValueText.color = Color.white;
        RectTransform fvRT = finalValueGO.GetComponent<RectTransform>();
        fvRT.anchorMin = new Vector2(0f, 0.5f); fvRT.anchorMax = new Vector2(1f, 0.5f);
        fvRT.anchoredPosition = new Vector2(0f, -45f); fvRT.sizeDelta = new Vector2(0f, 80f);

        GameObject restartGO = new GameObject("RestartButton");
        restartGO.transform.SetParent(resultPanel.transform, false);
        Image restartImg = restartGO.AddComponent<Image>();
        restartImg.color = new Color(0.20f, 0.60f, 0.90f);
        Button restartBtn = restartGO.AddComponent<Button>();
        restartBtn.targetGraphic = restartImg;
        ColorBlock rCB = restartBtn.colors;
        rCB.highlightedColor = new Color(0.35f, 0.72f, 1f);
        rCB.pressedColor   = new Color(0.12f, 0.45f, 0.75f);
        restartBtn.colors = rCB;
        RectTransform rbRT = restartGO.GetComponent<RectTransform>();
        rbRT.anchorMin = rbRT.anchorMax = new Vector2(0.5f, 0.5f);
        rbRT.anchoredPosition = new Vector2(0f, -130f); rbRT.sizeDelta = new Vector2(260f, 72f);
        restartBtn.onClick.AddListener(RestartGame);

        GameObject restartTextGO = new GameObject("RestartText");
        restartTextGO.transform.SetParent(restartGO.transform, false);
        Text restartBtnText = restartTextGO.AddComponent<Text>();
        restartBtnText.text = "PLAY AGAIN";
        restartBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartBtnText.fontSize = 34; restartBtnText.fontStyle = FontStyle.Bold;
        restartBtnText.alignment = TextAnchor.MiddleCenter; restartBtnText.color = Color.white;
        RectTransform rbtRT = restartTextGO.GetComponent<RectTransform>();
        rbtRT.anchorMin = Vector2.zero; rbtRT.anchorMax = Vector2.one;
        rbtRT.offsetMin = rbtRT.offsetMax = Vector2.zero;

        resultPanel.SetActive(false);

        // Shuffle notice
        GameObject shuffleNoticeGO = new GameObject("ShuffleNotice");
        shuffleNoticeGO.transform.SetParent(canvasGO.transform, false);
        shuffleNoticeText = shuffleNoticeGO.AddComponent<Text>();
        shuffleNoticeText.text = "Reshuffling...";
        shuffleNoticeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        shuffleNoticeText.fontSize = 52; shuffleNoticeText.fontStyle = FontStyle.Bold;
        shuffleNoticeText.alignment = TextAnchor.MiddleCenter;
        shuffleNoticeText.color = new Color(1f, 0.80f, 0.10f, 0f);
        shuffleNoticeText.raycastTarget = false;
        RectTransform snRT = shuffleNoticeGO.GetComponent<RectTransform>();
        snRT.anchorMin = snRT.anchorMax = new Vector2(0.5f, 0.5f);
        snRT.anchoredPosition = Vector2.zero; snRT.sizeDelta = new Vector2(500f, 80f);

        // EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }
    }

    void MakeStatCard(Transform parent, string label, string initialValue, Color accent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        out Text labelText, out Text valueText)
    {
        GameObject card = new GameObject(label + "Card");
        card.transform.SetParent(parent, false);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.45f);
        RectTransform cRT = card.GetComponent<RectTransform>();
        cRT.anchorMin = anchorMin; cRT.anchorMax = anchorMax;
        cRT.pivot = pivot; cRT.anchoredPosition = anchoredPos; cRT.sizeDelta = size;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(card.transform, false);
        labelText = labelGO.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 20; labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(accent.r, accent.g, accent.b, 0.95f);
        labelText.raycastTarget = false;
        RectTransform lRT = labelGO.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.54f); lRT.anchorMax = new Vector2(1f, 1f);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;

        GameObject valueGO = new GameObject("Value");
        valueGO.transform.SetParent(card.transform, false);
        valueText = valueGO.AddComponent<Text>();
        valueText.text = initialValue;
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 44; valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleCenter; valueText.color = Color.white;
        valueText.raycastTarget = false;
        RectTransform vRT = valueGO.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f); vRT.anchorMax = new Vector2(1f, 0.54f);
        vRT.offsetMin = vRT.offsetMax = Vector2.zero;
    }

    // ── Board logic ────────────────────────────────────────────
    void InitBoard()
    {
        board = new int[width, height];
        gems  = new GameObject[width, height];
        do
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    board[x, y] = RandomColorNoMatch(x, y);
        }
        while (!HasValidMove());
    }

    int RandomColorNoMatch(int x, int y)
    {
        List<int> forbidden = new List<int>();
        if (x >= 2 && board[x-1,y] == board[x-2,y]) forbidden.Add(board[x-1,y]);
        if (y >= 2 && board[x,y-1] == board[x,y-2]) forbidden.Add(board[x,y-1]);
        int color;
        do { color = Random.Range(0, gemColors.Length); }
        while (forbidden.Contains(color));
        return color;
    }

    void DrawBoard()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                CreateGem(x, y);
    }

    // Grid (x,y) → world position (centered board)
    Vector3 GemWorldPosition(int x, int y)
    {
        return new Vector3(
            (x - (width  - 1) * 0.5f) * CELL,
            ((height - 1) * 0.5f + BOARD_OFFSET_Y - y) * CELL,
            0f);
    }

    // World position of gem center (for VFX spawning)
    Vector3 GemWorldCenter(int x, int y)
    {
        if (gems[x, y] != null) return gems[x, y].transform.position;
        return GemWorldPosition(x, y);
    }

    void CreateGem(int x, int y)
    {
        if (gems[x, y] != null) Destroy(gems[x, y]);

        int ci = board[x, y];

        // Container holds world position — prefab animations only affect the child
        GameObject gem = new GameObject($"Gem_{x}_{y}");
        gem.transform.position = GemWorldPosition(x, y);

        GameObject prefab = (gemPrefabs != null && ci < gemPrefabs.Length) ? gemPrefabs[ci] : null;
        if (prefab != null)
        {
            GameObject visual = Instantiate(prefab, gem.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Auto-scale to fit one cell
            SpriteRenderer mainSR = visual.GetComponentInChildren<SpriteRenderer>();
            if (mainSR != null && mainSR.sprite != null)
            {
                float nativeSize = Mathf.Max(mainSR.sprite.bounds.size.x, mainSR.sprite.bounds.size.y);
                if (nativeSize > 0.01f)
                    visual.transform.localScale = Vector3.one * (CELL * 0.88f / nativeSize);
            }

            foreach (var sr in visual.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = 10;
        }
        else
        {
            SpriteRenderer sr = gem.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color  = gemColors[ci];
            sr.sortingOrder = 10;
        }

        var col = gem.AddComponent<BoxCollider2D>();
        col.size = new Vector2(CELL * 0.92f, CELL * 0.92f);

        gems[x, y] = gem;
    }

    bool ClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        return false;
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return Input.GetMouseButtonDown(0);
#endif
    }

    Vector2 CurrentMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
            return touch.primaryTouch.position.ReadValue();
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
        return Vector2.zero;
#else
        if (Input.touchCount > 0) return Input.GetTouch(0).position;
        return Input.mousePosition;
#endif
    }

    Sprite CreateCircleSprite()
    {
        int sz = 64;
        Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = sz / 2f - 0.5f, cy = sz / 2f - 0.5f, r = sz / 2f - 1f;
        for (int px = 0; px < sz; px++)
            for (int py = 0; py < sz; py++)
            {
                float dx = px - cx, dy = py - cy;
                float t = Mathf.Sqrt(dx*dx + dy*dy) / r;
                float b = Mathf.Lerp(1f, 0.3f, t * t);
                tex.SetPixel(px, py, t <= 1f ? new Color(b, b, b, 1f) : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
    }

    // ── Input / selection ──────────────────────────────────────
    void OnGemClick(int x, int y)
    {
        if (isProcessing || isGameOver) return;
        CancelHint();
        if (selectedX == -1)
        {
            selectedX = x; selectedY = y;
            HighlightGem(x, y, true);
        }
        else
        {
            HighlightGem(selectedX, selectedY, false);
            if (IsAdjacent(selectedX, selectedY, x, y))
                StartCoroutine(TrySwap(selectedX, selectedY, x, y));
            else
            {
                selectedX = x; selectedY = y;
                HighlightGem(x, y, true);
                return;
            }
            selectedX = selectedY = -1;
        }
    }

    void HighlightGem(int x, int y, bool on)
    {
        if (gems[x, y] == null) return;
        gems[x, y].transform.localScale = on ? new Vector3(1.15f, 1.15f, 1.15f) : Vector3.one;
    }

    bool IsAdjacent(int x1, int y1, int x2, int y2)
        => (Mathf.Abs(x1-x2) == 1 && y1 == y2) || (Mathf.Abs(y1-y2) == 1 && x1 == x2);

    // ── Swap & match ───────────────────────────────────────────
    IEnumerator TrySwap(int x1, int y1, int x2, int y2)
    {
        isProcessing = true;
        SwapBoard(x1, y1, x2, y2);
        yield return StartCoroutine(AnimateSwap(x1, y1, x2, y2));

        movesLeft--;
        UpdateMovesDisplay();

        List<Vector2Int> matches = FindMatches();
        if (matches.Count == 0)
        {
            SwapBoard(x1, y1, x2, y2);
            yield return StartCoroutine(AnimateSwap(x1, y1, x2, y2));
            CheckGameState();
        }
        else
        {
            ApplySpecialEffects(matches, x2, y2);
            yield return StartCoroutine(ProcessMatches(matches, 1));
            CheckGameState();
            if (!isGameOver && !HasValidMove())
                yield return StartCoroutine(ShuffleBoardCoroutine());
        }
        isProcessing = false;
    }

    void SwapBoard(int x1, int y1, int x2, int y2)
    {
        int tmp = board[x1, y1];
        board[x1, y1] = board[x2, y2];
        board[x2, y2] = tmp;
    }

    IEnumerator AnimateSwap(int x1, int y1, int x2, int y2)
    {
        if (gems[x1,y1] == null || gems[x2,y2] == null) yield break;
        Vector3 pos1 = gems[x1,y1].transform.position;
        Vector3 pos2 = gems[x2,y2].transform.position;
        float t = 0, dur = 0.18f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            if (gems[x1,y1]) gems[x1,y1].transform.position = Vector3.Lerp(pos1, pos2, p);
            if (gems[x2,y2]) gems[x2,y2].transform.position = Vector3.Lerp(pos2, pos1, p);
            yield return null;
        }
        if (gems[x1,y1]) gems[x1,y1].transform.position = pos2;
        if (gems[x2,y2]) gems[x2,y2].transform.position = pos1;
        GameObject tmp = gems[x1, y1];
        gems[x1, y1] = gems[x2, y2];
        gems[x2, y2] = tmp;
        if (gems[x1,y1]) gems[x1,y1].name = $"Gem_{x1}_{y1}";
        if (gems[x2,y2]) gems[x2,y2].name = $"Gem_{x2}_{y2}";
    }

    List<Vector2Int> FindMatches()
    {
        HashSet<Vector2Int> matched = new HashSet<Vector2Int>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width - 2; x++)
                if (board[x,y] == board[x+1,y] && board[x,y] == board[x+2,y])
                    for (int i = 0; i < 3; i++) matched.Add(new Vector2Int(x+i, y));
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height - 2; y++)
                if (board[x,y] == board[x,y+1] && board[x,y] == board[x,y+2])
                    for (int i = 0; i < 3; i++) matched.Add(new Vector2Int(x, y+i));
        return new List<Vector2Int>(matched);
    }

    IEnumerator ProcessMatches(List<Vector2Int> matches, int chain = 1)
    {
        int points = matches.Count * 10 * chain;
        score += points;
        if (scoreText) scoreText.text = score.ToString();
        StartCoroutine(ScorePop());
        SpawnFloatingScore(matches, points, chain);

        foreach (var m in matches)
        {
            int color = board[m.x, m.y];
            Vector3 wpos = GemWorldCenter(m.x, m.y);
            board[m.x, m.y] = -1;
            if (gems[m.x, m.y] != null)
                StartCoroutine(ScaleOutAndDestroy(gems[m.x, m.y], color, wpos));
        }
        yield return new WaitForSeconds(0.3f);
        foreach (var m in matches) gems[m.x, m.y] = null;

        yield return StartCoroutine(DropGems());

        List<Vector2Int> newMatches = FindMatches();
        if (newMatches.Count > 0)
        {
            ApplySpecialEffects(newMatches, -1, -1);
            yield return StartCoroutine(ProcessMatches(newMatches, chain + 1));
        }
    }

    IEnumerator ScaleOutAndDestroy(GameObject gem, int colorIndex, Vector3 worldPos)
    {
        VFXManager.Instance?.PlayDestroyFX(colorIndex, worldPos);
        float t = 0, dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0f, t / dur);
            if (gem) gem.transform.localScale = new Vector3(s, s, s);
            yield return null;
        }
        if (gem) Destroy(gem);
    }

    void ApplySpecialEffects(List<Vector2Int> matches, int prefX, int prefY)
    {
        if (matches.Count < 4) return;

        int bestLen = 0; bool bestIsH = true; Vector2Int pivot = new Vector2Int(prefX, prefY);

        var byRow = new Dictionary<int, List<int>>();
        foreach (var m in matches) { if (!byRow.ContainsKey(m.y)) byRow[m.y] = new List<int>(); byRow[m.y].Add(m.x); }
        foreach (var kv in byRow)
        {
            var xs = kv.Value; xs.Sort(); int run = 1;
            for (int i = 1; i < xs.Count; i++)
            {
                run = (xs[i] == xs[i-1] + 1) ? run + 1 : 1;
                if (run > bestLen)
                {
                    bestLen = run; bestIsH = true;
                    int sx = xs[i - run + 1];
                    pivot = (kv.Key == prefY && prefX >= sx && prefX <= xs[i])
                        ? new Vector2Int(prefX, prefY) : new Vector2Int(xs[i - run/2], kv.Key);
                }
            }
        }

        var byCol = new Dictionary<int, List<int>>();
        foreach (var m in matches) { if (!byCol.ContainsKey(m.x)) byCol[m.x] = new List<int>(); byCol[m.x].Add(m.y); }
        foreach (var kv in byCol)
        {
            var ys = kv.Value; ys.Sort(); int run = 1;
            for (int i = 1; i < ys.Count; i++)
            {
                run = (ys[i] == ys[i-1] + 1) ? run + 1 : 1;
                if (run > bestLen)
                {
                    bestLen = run; bestIsH = false;
                    int sy = ys[i - run + 1];
                    pivot = (kv.Key == prefX && prefY >= sy && prefY <= ys[i])
                        ? new Vector2Int(prefX, prefY) : new Vector2Int(kv.Key, ys[i - run/2]);
                }
            }
        }

        if (bestLen < 4) return;

        Vector3 pivotWorld = GemWorldCenter(pivot.x, pivot.y);
        HashSet<Vector2Int> bonus = new HashSet<Vector2Int>();

        if (bestLen >= 5)
        {
            if (bestIsH) for (int bx = 0; bx < width; bx++)  bonus.Add(new Vector2Int(bx, pivot.y));
            else         for (int by = 0; by < height; by++) bonus.Add(new Vector2Int(pivot.x, by));
            VFXManager.Instance?.PlayRocketFX(pivotWorld, bestIsH);
        }
        else
        {
            for (int bx = pivot.x - 1; bx <= pivot.x + 1; bx++)
                for (int by = pivot.y - 1; by <= pivot.y + 1; by++)
                    if (bx >= 0 && bx < width && by >= 0 && by < height)
                        bonus.Add(new Vector2Int(bx, by));
            VFXManager.Instance?.PlayBombFX(pivotWorld);
        }

        foreach (var b in bonus)
            if (!matches.Contains(b) && board[b.x, b.y] != -1)
                matches.Add(b);
    }

    // ── Drop & animate ─────────────────────────────────────────
    IEnumerator DropGems()
    {
        float maxDur = 0f;

        for (int x = 0; x < width; x++)
        {
            List<int> cols = new List<int>();
            List<GameObject> gos = new List<GameObject>();
            for (int y = height - 1; y >= 0; y--)
            {
                if (board[x, y] == -1) continue;
                cols.Add(board[x, y]); gos.Add(gems[x, y]);
                board[x, y] = -1; gems[x, y] = null;
            }

            int empty = height - cols.Count;

            for (int i = 0; i < cols.Count; i++)
            {
                int y = height - 1 - i;
                board[x, y] = cols[i]; gems[x, y] = gos[i];
                if (gems[x, y] != null)
                {
                    gems[x, y].name = $"Gem_{x}_{y}";
                    Vector3 dst = GemWorldPosition(x, y);
                    float dur = DropDuration(gems[x, y], dst);
                    maxDur = Mathf.Max(maxDur, dur);
                    StartCoroutine(AnimateDrop(gems[x, y], dst));
                }
            }

            for (int i = 0; i < empty; i++)
            {
                int y = i;
                board[x, y] = Random.Range(0, gemColors.Length);
                CreateGem(x, y);
                if (gems[x, y] != null)
                {
                    Vector3 dst = GemWorldPosition(x, y);
                    gems[x, y].transform.position = new Vector3(dst.x, dst.y + (empty - i) * CELL, 0f);
                    float dur = DropDuration(gems[x, y], dst);
                    maxDur = Mathf.Max(maxDur, dur);
                    StartCoroutine(AnimateDrop(gems[x, y], dst));
                }
            }
        }

        yield return new WaitForSeconds(maxDur + 0.05f);
    }

    float DropDuration(GameObject gem, Vector3 targetPos)
    {
        if (gem == null) return 0f;
        float dist = Mathf.Abs(gem.transform.position.y - targetPos.y);
        return Mathf.Clamp(dist / 12f, 0.06f, 0.40f);
    }

    IEnumerator AnimateDrop(GameObject gem, Vector3 targetPos)
    {
        if (gem == null) yield break;
        Vector3 from = gem.transform.position;
        float dur = DropDuration(gem, targetPos);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (gem == null) yield break;
            gem.transform.position = Vector3.Lerp(from, targetPos, (t / dur) * (t / dur));
            yield return null;
        }
        if (gem) gem.transform.position = targetPos;
    }

    // ── Hint system ────────────────────────────────────────────
    bool FindHintMove(out int x1, out int y1, out int x2, out int y2)
    {
        x1 = y1 = x2 = y2 = -1;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x + 1 < width)  { SwapBoard(x,y,x+1,y); bool ok=FindMatches().Count>0; SwapBoard(x,y,x+1,y); if(ok){x1=x;y1=y;x2=x+1;y2=y;return true;} }
                if (y + 1 < height) { SwapBoard(x,y,x,y+1); bool ok=FindMatches().Count>0; SwapBoard(x,y,x,y+1); if(ok){x1=x;y1=y;x2=x;y2=y+1;return true;} }
            }
        return false;
    }

    void ShowHint()
    {
        if (!FindHintMove(out hintX1, out hintY1, out hintX2, out hintY2)) return;
        hintCoroutine = StartCoroutine(PulseHint(hintX1, hintY1, hintX2, hintY2));
    }

    void CancelHint()
    {
        idleTime = 0f;
        if (hintCoroutine == null) return;
        StopCoroutine(hintCoroutine);
        hintCoroutine = null;
        if (hintX1 >= 0 && gems[hintX1, hintY1]) gems[hintX1, hintY1].transform.localScale = Vector3.one;
        if (hintX2 >= 0 && gems[hintX2, hintY2]) gems[hintX2, hintY2].transform.localScale = Vector3.one;
    }

    IEnumerator PulseHint(int x1, int y1, int x2, int y2)
    {
        for (int i = 0; i < 3; i++)
        {
            float dur = 0.5f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float s = 1f + 0.22f * Mathf.Sin(t / dur * Mathf.PI);
                if (gems[x1,y1]) gems[x1,y1].transform.localScale = new Vector3(s, s, s);
                if (gems[x2,y2]) gems[x2,y2].transform.localScale = new Vector3(s, s, s);
                yield return null;
            }
            if (gems[x1,y1]) gems[x1,y1].transform.localScale = Vector3.one;
            if (gems[x2,y2]) gems[x2,y2].transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.15f);
        }
        hintCoroutine = null;
        idleTime = 3f;
    }

    // ── Floating score ─────────────────────────────────────────
    void SpawnFloatingScore(List<Vector2Int> cleared, int points, int chain)
    {
        if (canvas == null || cleared.Count == 0) return;

        Vector3 worldCenter = Vector3.zero;
        foreach (var m in cleared) worldCenter += GemWorldCenter(m.x, m.y);
        worldCenter /= cleared.Count;

        Vector2 screenPos = gameCamera.WorldToScreenPoint(worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, gameCamera, out Vector2 canvasPos);

        GameObject go = new GameObject("FloatScore");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        Text txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        if      (chain >= 3) { txt.text = $"+{points}\n×{chain} CHAIN!"; txt.fontSize = 38; txt.color = new Color(1f, 0.55f, 0.1f); }
        else if (chain == 2) { txt.text = $"+{points}\n×2 CHAIN!";       txt.fontSize = 36; txt.color = new Color(1f, 0.88f, 0.1f); }
        else                 { txt.text = $"+{points}";                   txt.fontSize = 32; txt.color = Color.white; }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 80f);
        rt.anchoredPosition = canvasPos;

        StartCoroutine(FloatAndFade(go, txt, canvasPos));
    }

    IEnumerator FloatAndFade(GameObject go, Text txt, Vector2 startPos)
    {
        float dur = 1.0f;
        RectTransform rt = go ? go.GetComponent<RectTransform>() : null;
        Color c0 = txt ? txt.color : Color.white;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (go == null) yield break;
            float p = t / dur;
            rt.anchoredPosition = startPos + new Vector2(0f, 120f * p);
            txt.color = new Color(c0.r, c0.g, c0.b, 1f - p);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── Shuffle ────────────────────────────────────────────────
    void ShuffleBoard()
    {
        List<int> colors = new List<int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                colors.Add(board[x, y]);
        do
        {
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = colors[i]; colors[i] = colors[j]; colors[j] = tmp;
            }
            int idx = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    board[x, y] = colors[idx++];
        }
        while (FindMatches().Count > 0 || !HasValidMove());

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (gems[x, y] != null) Destroy(gems[x, y]);
                CreateGem(x, y);
            }
    }

    IEnumerator ShuffleBoardCoroutine()
    {
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, t / 0.3f);
            yield return null;
        }
        ShuffleBoard();
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, 1f - t / 0.3f);
            yield return null;
        }
        shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, 0f);
    }

    // ── Game state ─────────────────────────────────────────────
    bool HasValidMove()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x+1 < width)  { SwapBoard(x,y,x+1,y); bool ok=FindMatches().Count>0; SwapBoard(x,y,x+1,y); if(ok) return true; }
                if (y+1 < height) { SwapBoard(x,y,x,y+1); bool ok=FindMatches().Count>0; SwapBoard(x,y,x,y+1); if(ok) return true; }
            }
        return false;
    }

    void CheckGameState()
    {
        if (isGameOver) return;
        if (score >= targetScore)      { isGameOver = true; ShowResult(true);  }
        else if (movesLeft <= 0)       { isGameOver = true; ShowResult(false); }
    }

    void ShowResult(bool win)
    {
        isProcessing = true;
        resultText.text  = win ? "YOU WIN!" : "GAME OVER";
        resultText.color = win ? new Color(1f, 0.88f, 0.1f) : new Color(0.95f, 0.3f, 0.3f);
        if (finalScoreValueText) finalScoreValueText.text = score.ToString();
        resultPanel.transform.SetAsLastSibling();
        resultPanel.SetActive(true);
        if (win) VFXManager.Instance?.PlayWinFX();
    }

    void RestartGame()
    {
        VFXManager.Instance?.StopWinFX();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null) { Destroy(gems[x, y]); gems[x, y] = null; }

        score = 0; movesLeft = maxMoves;
        isGameOver = false; isProcessing = false;
        selectedX = selectedY = -1;

        if (scoreText) scoreText.text = "0";
        if (movesText) { movesText.text = maxMoves.ToString(); movesText.color = Color.white; }
        resultPanel.SetActive(false);
        InitBoard(); DrawBoard();
    }

    // ── UI helpers ─────────────────────────────────────────────
    void UpdateMovesDisplay()
    {
        if (!movesText) return;
        movesText.text = movesLeft.ToString();
        if      (movesLeft <= 5)  movesText.color = new Color(0.95f, 0.30f, 0.30f);
        else if (movesLeft <= 10) movesText.color = new Color(0.95f, 0.70f, 0.10f);
        else                      movesText.color = Color.white;
    }

    IEnumerator ScorePop()
    {
        if (!scoreText) yield break;
        float dur = 0.12f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float s = Mathf.Lerp(1f, 1.45f, t / dur);
            scoreText.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float s = Mathf.Lerp(1.45f, 1f, t / dur);
            scoreText.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        scoreText.transform.localScale = Vector3.one;
    }
}
