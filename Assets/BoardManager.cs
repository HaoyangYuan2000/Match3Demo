using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem.UI;

public class BoardManager : MonoBehaviour
{
    [Header("棋盘设置")]
    public int width = 7;
    public int height = 9;
    public float cellSize = 90f;

    [Header("方块颜色（6种）")]
    public Color[] gemColors = new Color[]
    {
        new Color(0.95f, 0.3f, 0.3f),
        new Color(0.3f, 0.65f, 0.95f),
        new Color(0.35f, 0.80f, 0.40f),
        new Color(0.95f, 0.80f, 0.2f),
        new Color(0.75f, 0.4f, 0.90f),
        new Color(0.95f, 0.60f, 0.2f),
    };

    [Header("游戏规则")]
    public int maxMoves = 30;
    public int targetScore = 500;

    [Header("UI")]
    public Text scoreText;

    private int[,] board;
    private GameObject[,] gems;
    private int selectedX = -1, selectedY = -1;
    private bool isProcessing = false;
    private bool isGameOver = false;
    private int score = 0;
    private int movesLeft;
    private Canvas canvas;
    private RectTransform boardRect;
    private Text movesText;
    private Text resultText;
    private Text finalScoreValueText;
    private GameObject resultPanel;
    private Text shuffleNoticeText;

    void Start()
    {
        movesLeft = maxMoves;
        SetupUI();
        InitBoard();
        DrawBoard();
    }

    void SetupUI()
    {
        GameObject canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = Vector2.zero;
        hRT.sizeDelta = new Vector2(0f, 130f);

        // Stat cards: GOAL | SCORE | MOVES
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

        // Board background
        GameObject boardBGGO = new GameObject("BoardBG");
        boardBGGO.transform.SetParent(canvasGO.transform, false);
        Image boardBGImg = boardBGGO.AddComponent<Image>();
        boardBGImg.color = new Color(0.09f, 0.06f, 0.20f);
        RectTransform bbRT = boardBGGO.GetComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0.5f, 0.5f);
        bbRT.anchorMax = new Vector2(0.5f, 0.5f);
        bbRT.pivot = new Vector2(0.5f, 1f);
        bbRT.anchoredPosition = new Vector2(0f, 508f);
        bbRT.sizeDelta = new Vector2(width * cellSize + 16f, height * cellSize + 16f);

        // Result panel
        resultPanel = new GameObject("ResultPanel");
        resultPanel.transform.SetParent(canvasGO.transform, false);
        Image panelImg = resultPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.82f);
        RectTransform rpRT = resultPanel.GetComponent<RectTransform>();
        rpRT.anchorMin = Vector2.zero;
        rpRT.anchorMax = Vector2.one;
        rpRT.offsetMin = rpRT.offsetMax = Vector2.zero;

        GameObject resultTextGO = new GameObject("ResultText");
        resultTextGO.transform.SetParent(resultPanel.transform, false);
        resultText = resultTextGO.AddComponent<Text>();
        resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultText.fontSize = 90;
        resultText.fontStyle = FontStyle.Bold;
        resultText.alignment = TextAnchor.MiddleCenter;
        RectTransform rtRT = resultTextGO.GetComponent<RectTransform>();
        rtRT.anchorMin = new Vector2(0f, 0.5f);
        rtRT.anchorMax = new Vector2(1f, 0.5f);
        rtRT.anchoredPosition = new Vector2(0f, 90f);
        rtRT.sizeDelta = new Vector2(0f, 120f);

        GameObject finalLabelGO = new GameObject("FinalScoreLabel");
        finalLabelGO.transform.SetParent(resultPanel.transform, false);
        Text finalLabel = finalLabelGO.AddComponent<Text>();
        finalLabel.text = "FINAL SCORE";
        finalLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finalLabel.fontSize = 28;
        finalLabel.alignment = TextAnchor.MiddleCenter;
        finalLabel.color = new Color(0.60f, 0.60f, 0.60f);
        RectTransform flRT = finalLabelGO.GetComponent<RectTransform>();
        flRT.anchorMin = new Vector2(0f, 0.5f);
        flRT.anchorMax = new Vector2(1f, 0.5f);
        flRT.anchoredPosition = new Vector2(0f, 10f);
        flRT.sizeDelta = new Vector2(0f, 40f);

        GameObject finalValueGO = new GameObject("FinalScoreValue");
        finalValueGO.transform.SetParent(resultPanel.transform, false);
        finalScoreValueText = finalValueGO.AddComponent<Text>();
        finalScoreValueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finalScoreValueText.fontSize = 64;
        finalScoreValueText.fontStyle = FontStyle.Bold;
        finalScoreValueText.alignment = TextAnchor.MiddleCenter;
        finalScoreValueText.color = Color.white;
        RectTransform fvRT = finalValueGO.GetComponent<RectTransform>();
        fvRT.anchorMin = new Vector2(0f, 0.5f);
        fvRT.anchorMax = new Vector2(1f, 0.5f);
        fvRT.anchoredPosition = new Vector2(0f, -45f);
        fvRT.sizeDelta = new Vector2(0f, 80f);

        GameObject restartGO = new GameObject("RestartButton");
        restartGO.transform.SetParent(resultPanel.transform, false);
        Image restartImg = restartGO.AddComponent<Image>();
        restartImg.color = new Color(0.20f, 0.60f, 0.90f);
        Button restartBtn = restartGO.AddComponent<Button>();
        restartBtn.targetGraphic = restartImg;
        ColorBlock rCB = restartBtn.colors;
        rCB.highlightedColor = new Color(0.35f, 0.72f, 1f);
        rCB.pressedColor = new Color(0.12f, 0.45f, 0.75f);
        restartBtn.colors = rCB;
        RectTransform rbRT = restartGO.GetComponent<RectTransform>();
        rbRT.anchorMin = rbRT.anchorMax = new Vector2(0.5f, 0.5f);
        rbRT.anchoredPosition = new Vector2(0f, -130f);
        rbRT.sizeDelta = new Vector2(260f, 72f);
        restartBtn.onClick.AddListener(RestartGame);

        GameObject restartTextGO = new GameObject("RestartText");
        restartTextGO.transform.SetParent(restartGO.transform, false);
        Text restartBtnText = restartTextGO.AddComponent<Text>();
        restartBtnText.text = "PLAY AGAIN";
        restartBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartBtnText.fontSize = 34;
        restartBtnText.fontStyle = FontStyle.Bold;
        restartBtnText.alignment = TextAnchor.MiddleCenter;
        restartBtnText.color = Color.white;
        RectTransform rbtRT = restartTextGO.GetComponent<RectTransform>();
        rbtRT.anchorMin = Vector2.zero;
        rbtRT.anchorMax = Vector2.one;
        rbtRT.offsetMin = rbtRT.offsetMax = Vector2.zero;

        resultPanel.SetActive(false);

        // Shuffle notice
        GameObject shuffleNoticeGO = new GameObject("ShuffleNotice");
        shuffleNoticeGO.transform.SetParent(canvasGO.transform, false);
        shuffleNoticeText = shuffleNoticeGO.AddComponent<Text>();
        shuffleNoticeText.text = "Reshuffling...";
        shuffleNoticeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        shuffleNoticeText.fontSize = 52;
        shuffleNoticeText.fontStyle = FontStyle.Bold;
        shuffleNoticeText.alignment = TextAnchor.MiddleCenter;
        shuffleNoticeText.color = new Color(1f, 0.80f, 0.10f, 0f);
        RectTransform snRT = shuffleNoticeGO.GetComponent<RectTransform>();
        snRT.anchorMin = snRT.anchorMax = new Vector2(0.5f, 0.5f);
        snRT.anchoredPosition = Vector2.zero;
        snRT.sizeDelta = new Vector2(500f, 80f);

        // Board container
        GameObject boardGO = new GameObject("Board");
        boardGO.transform.SetParent(canvasGO.transform, false);
        boardRect = boardGO.AddComponent<RectTransform>();
        boardRect.anchorMin = new Vector2(0.5f, 0.5f);
        boardRect.anchorMax = new Vector2(0.5f, 0.5f);
        boardRect.pivot = new Vector2(0.5f, 1f);
        boardRect.anchoredPosition = new Vector2(0f, 500f);
        boardRect.sizeDelta = new Vector2(width * cellSize, height * cellSize);

        // EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
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
        RectTransform lRT = labelGO.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.54f); lRT.anchorMax = new Vector2(1f, 1f);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;

        GameObject valueGO = new GameObject("Value");
        valueGO.transform.SetParent(card.transform, false);
        valueText = valueGO.AddComponent<Text>();
        valueText.text = initialValue;
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontSize = 44; valueText.fontStyle = FontStyle.Bold;
        valueText.alignment = TextAnchor.MiddleCenter;
        valueText.color = Color.white;
        RectTransform vRT = valueGO.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f); vRT.anchorMax = new Vector2(1f, 0.54f);
        vRT.offsetMin = vRT.offsetMax = Vector2.zero;
    }

    void InitBoard()
    {
        board = new int[width, height];
        gems = new GameObject[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                board[x, y] = RandomColorNoMatch(x, y);
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

    // 把逻辑坐标(x,y) 转换成 anchoredPosition
    // pivot 在棋盘左上角，X向右，Y向下（负方向）
    Vector2 GemPosition(int x, int y)
    {
        float px = x * cellSize + 4f;
        float py = -(y * cellSize + 4f);   // Y 向下为正，UI 向上为正，所以取负
        return new Vector2(px, py);
    }

    void CreateGem(int x, int y)
    {
        if (gems[x, y] != null) Destroy(gems[x, y]);

        GameObject gem = new GameObject($"Gem_{x}_{y}");
        gem.transform.SetParent(boardRect, false);

        RectTransform rt = gem.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(cellSize - 8, cellSize - 8);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // 锚点左上
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = GemPosition(x, y);

        Image img = gem.AddComponent<Image>();
        img.color = gemColors[board[x, y]];
        img.sprite = CreateGemSprite(board[x, y]);

        // Highlight
        GameObject shine = new GameObject("Shine");
        shine.transform.SetParent(gem.transform, false);
        Image shineImg = shine.AddComponent<Image>();
        shineImg.color = new Color(1f, 1f, 1f, 0.30f);
        RectTransform sRT = shine.GetComponent<RectTransform>();
        sRT.sizeDelta = new Vector2(20, 20);
        sRT.anchorMin = sRT.anchorMax = new Vector2(0.25f, 0.75f);
        sRT.anchoredPosition = Vector2.zero;

        Button btn = gem.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;

        int gx = x, gy = y;
        btn.onClick.AddListener(() => OnGemClick(gx, gy));
        gems[x, y] = gem;
    }

    void UpdateGemListener(int x, int y)
    {
        if (gems[x, y] == null) return;
        Button btn = gems[x, y].GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        int gx = x, gy = y;
        btn.onClick.AddListener(() => OnGemClick(gx, gy));
    }

    Sprite CreateGemSprite(int colorIndex)
    {
        int size = 80;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = size / 2f - 0.5f, cy = size / 2f - 0.5f;
        float cornerR = 16f, symR = size * 0.24f;
        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
            {
                float rdx = Mathf.Max(0, Mathf.Abs(px - cx) - (cx - cornerR));
                float rdy = Mathf.Max(0, Mathf.Abs(py - cy) - (cy - cornerR));
                if (rdx*rdx + rdy*rdy > cornerR*cornerR) { tex.SetPixel(px, py, Color.clear); continue; }
                float dx = px - cx, dy = py - cy;
                float b = IsInSymbol(colorIndex, dx, dy, symR) ? 1.0f : 0.75f;
                tex.SetPixel(px, py, new Color(b, b, b, 1f));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    bool IsInSymbol(int index, float dx, float dy, float r)
    {
        switch (index % 6)
        {
            case 0: // Red — circle
                return dx*dx + dy*dy <= r*r;
            case 1: // Blue — diamond
                return Mathf.Abs(dx) + Mathf.Abs(dy) <= r * 1.15f;
            case 2: // Green — triangle (up)
            {
                float topY = r * 0.72f, botY = -r * 0.55f;
                if (dy < botY || dy > topY) return false;
                return Mathf.Abs(dx) <= (topY - dy) / (topY - botY) * r;
            }
            case 3: // Yellow — 4-point star
            {
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                if (dist > r) return false;
                float angle = Mathf.Atan2(dy, dx);
                float inner = r * 0.42f;
                return dist <= inner + (r - inner) * Mathf.Abs(Mathf.Cos(2f * angle));
            }
            case 4: // Purple — cross
                return (Mathf.Abs(dx) <= r*0.30f && Mathf.Abs(dy) <= r*0.95f) ||
                       (Mathf.Abs(dy) <= r*0.30f && Mathf.Abs(dx) <= r*0.95f);
            case 5: // Orange — hexagon
            {
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                if (dist < 0.001f) return true;
                float angle = Mathf.Atan2(dy, dx);
                float sector = 2f * Mathf.PI / 6f;
                float nearest = Mathf.Round(angle / sector) * sector;
                return dist <= r * Mathf.Cos(sector * 0.5f) / Mathf.Cos(angle - nearest);
            }
            default: return false;
        }
    }

    void OnGemClick(int x, int y)
    {
        if (isProcessing || isGameOver) return;
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
        gems[x, y].GetComponent<RectTransform>().localScale =
            on ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
    }

    bool IsAdjacent(int x1, int y1, int x2, int y2)
        => (Mathf.Abs(x1-x2) == 1 && y1 == y2) || (Mathf.Abs(y1-y2) == 1 && x1 == x2);

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
            yield return StartCoroutine(ProcessMatches(matches));
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
        RectTransform rt1 = gems[x1,y1].GetComponent<RectTransform>();
        RectTransform rt2 = gems[x2,y2].GetComponent<RectTransform>();
        Vector2 pos1 = rt1.anchoredPosition, pos2 = rt2.anchoredPosition;
        float t = 0, dur = 0.18f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt1.anchoredPosition = Vector2.Lerp(pos1, pos2, p);
            rt2.anchoredPosition = Vector2.Lerp(pos2, pos1, p);
            yield return null;
        }
        rt1.anchoredPosition = pos2;
        rt2.anchoredPosition = pos1;
        GameObject tmp = gems[x1, y1];
        gems[x1, y1] = gems[x2, y2];
        gems[x2, y2] = tmp;
        gems[x1,y1].name = $"Gem_{x1}_{y1}";
        gems[x2,y2].name = $"Gem_{x2}_{y2}";
        UpdateGemListener(x1, y1);
        UpdateGemListener(x2, y2);
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

    IEnumerator ProcessMatches(List<Vector2Int> matches)
    {
        score += matches.Count * 10;
        if (scoreText) scoreText.text = score.ToString();
        StartCoroutine(ScorePop());

        foreach (var m in matches)
        {
            board[m.x, m.y] = -1;
            if (gems[m.x, m.y] != null)
                StartCoroutine(ScaleOutAndDestroy(gems[m.x, m.y]));
        }
        yield return new WaitForSeconds(0.3f);
        foreach (var m in matches) gems[m.x, m.y] = null;

        yield return StartCoroutine(DropGems());

        List<Vector2Int> newMatches = FindMatches();
        if (newMatches.Count > 0)
        {
            ApplySpecialEffects(newMatches, -1, -1);
            yield return StartCoroutine(ProcessMatches(newMatches));
        }
    }

    IEnumerator ScaleOutAndDestroy(GameObject gem)
    {
        float t = 0, dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0f, t / dur);
            if (gem) gem.transform.localScale = new Vector3(s, s, 1);
            yield return null;
        }
        if (gem) Destroy(gem);
    }

    void ApplySpecialEffects(List<Vector2Int> matches, int prefX, int prefY)
    {
        if (matches.Count < 4) return;

        // Find the longest consecutive run in the match set
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

        // Immediately expand the match list
        HashSet<Vector2Int> bonus = new HashSet<Vector2Int>();
        if (bestLen >= 5) // Rocket: clear full row or column
        {
            if (bestIsH)
                for (int bx = 0; bx < width; bx++) bonus.Add(new Vector2Int(bx, pivot.y));
            else
                for (int by = 0; by < height; by++) bonus.Add(new Vector2Int(pivot.x, by));
        }
        else // Bomb: clear 3×3
        {
            for (int bx = pivot.x - 1; bx <= pivot.x + 1; bx++)
                for (int by = pivot.y - 1; by <= pivot.y + 1; by++)
                    if (bx >= 0 && bx < width && by >= 0 && by < height)
                        bonus.Add(new Vector2Int(bx, by));
        }

        foreach (var b in bonus)
            if (!matches.Contains(b) && board[b.x, b.y] != -1)
                matches.Add(b);
    }

    void UpdateMovesDisplay()
    {
        if (!movesText) return;
        movesText.text = movesLeft.ToString();
        if (movesLeft <= 5)
            movesText.color = new Color(0.95f, 0.30f, 0.30f);
        else if (movesLeft <= 10)
            movesText.color = new Color(0.95f, 0.70f, 0.10f);
        else
            movesText.color = Color.white;
    }

    IEnumerator ScorePop()
    {
        if (!scoreText) yield break;
        float t = 0f, dur = 0.12f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 1.45f, t / dur);
            scoreText.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1.45f, 1f, t / dur);
            scoreText.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        scoreText.transform.localScale = Vector3.one;
    }

    bool HasValidMove()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x + 1 < width)
                {
                    SwapBoard(x, y, x + 1, y);
                    bool ok = FindMatches().Count > 0;
                    SwapBoard(x, y, x + 1, y);
                    if (ok) return true;
                }
                if (y + 1 < height)
                {
                    SwapBoard(x, y, x, y + 1);
                    bool ok = FindMatches().Count > 0;
                    SwapBoard(x, y, x, y + 1);
                    if (ok) return true;
                }
            }
        return false;
    }

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
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, t / 0.3f);
            yield return null;
        }
        ShuffleBoard();
        yield return new WaitForSeconds(0.5f);
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, 1f - t / 0.3f);
            yield return null;
        }
        shuffleNoticeText.color = new Color(1f, 0.8f, 0.1f, 0f);
    }

    void CheckGameState()
    {
        if (isGameOver) return;
        if (score >= targetScore)
        {
            isGameOver = true;
            ShowResult(true);
        }
        else if (movesLeft <= 0)
        {
            isGameOver = true;
            ShowResult(false);
        }
    }

    void ShowResult(bool win)
    {
        isProcessing = true;
        resultText.text = win ? "YOU WIN!" : "GAME OVER";
        resultText.color = win ? new Color(1f, 0.88f, 0.1f) : new Color(0.95f, 0.3f, 0.3f);
        if (finalScoreValueText) finalScoreValueText.text = score.ToString();
        resultPanel.transform.SetAsLastSibling();
        resultPanel.SetActive(true);
    }

    void RestartGame()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (gems[x, y] != null) { Destroy(gems[x, y]); gems[x, y] = null; }

        score = 0;
        movesLeft = maxMoves;
        isGameOver = false;
        isProcessing = false;
        selectedX = selectedY = -1;

        if (scoreText) scoreText.text = "0";
        if (movesText) { movesText.text = maxMoves.ToString(); movesText.color = Color.white; }
        resultPanel.SetActive(false);

        InitBoard();
        DrawBoard();
    }

    IEnumerator DropGems()
    {
        // 每列从底部往上找空格，把上方方块往下移
        for (int x = 0; x < width; x++)
        {
            // 从最底行(y = height-1)往上扫
            for (int y = height - 1; y >= 0; y--)
            {
                if (board[x, y] != -1) continue;

                // 找这个空格上方最近的非空方块
                for (int above = y - 1; above >= 0; above--)
                {
                    if (board[x, above] == -1) continue;

                    // 把 above 行的方块移到 y 行
                    board[x, y] = board[x, above];
                    board[x, above] = -1;

                    gems[x, y] = gems[x, above];
                    gems[x, above] = null;

                    if (gems[x, y] != null)
                    {
                        gems[x, y].name = $"Gem_{x}_{y}";
                        gems[x, y].GetComponent<RectTransform>().anchoredPosition = GemPosition(x, y);
                        UpdateGemListener(x, y);
                    }
                    break;
                }
            }

            // 顶部空格填新方块
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == -1)
                {
                    board[x, y] = Random.Range(0, gemColors.Length);
                    CreateGem(x, y);
                }
            }
        }
        yield return new WaitForSeconds(0.15f);
    }
}