// ============================================================================
// VILL4GE — AttacheCase.cs  (RE4-faithful)
// Maleta com grid, armas ocupam espaços diferentes, rotação de itens,
// upgrade de tamanho no merchant. Visual idêntico ao RE4.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class AttacheCase : MonoBehaviour
{
    public static AttacheCase I;

    // ── Grid ───────────────────────────────────────────────────────────────
    public int gridW = 10, gridH = 6;
    int[,] grid;  // 0=vazio, itemID para ocupado
    List<CaseItem> items = new List<CaseItem>();
    int nextId = 1;
    bool isOpen;
    int selectedIdx = -1;

    // ── UI ──────────────────────────────────────────────────────────────────
    Canvas canvas;
    GameObject casePanel;
    Image[,] cellImages;
    Image bgPanel;
    Text titleText, infoText, controlsText, sizeText;
    GameObject infoPanel;
    const int CELL = 48;
    const int PAD = 2;

    // ── Item Template ──────────────────────────────────────────────────────
    public class CaseItem
    {
        public int id;
        public string name;
        public ItemType type;
        public int w, h;       // tamanho no grid (colunas × linhas)
        public int gx, gy;     // posição top-left no grid
        public bool rotated;   // se rotacionou 90°
        public int weaponIdx;  // índice em Player.weapons (-1 se não for arma)
        public int quantity;   // para consumíveis (herbs, grenades, ammo)
        public Color color;

        public int ActualW => rotated ? h : w;
        public int ActualH => rotated ? w : h;
    }

    public enum ItemType { Weapon, Herb, Grenade, Ammo }

    // Definição de tamanhos RE4 para cada arma
    static readonly int[][] WeaponSizes = {
        new[]{3, 2},  // Handgun   3 cols × 2 rows
        new[]{8, 2},  // Shotgun   8 cols × 2 rows
        new[]{9, 1},  // Rifle     9 cols × 1 row
        new[]{5, 2},  // TMP       5 cols × 2 rows
        new[]{7, 2},  // Rocket    7 cols × 2 rows
    };
    static readonly Color[] WeaponColors = {
        new Color(.45f, .45f, .5f),    // Handgun  - cinza
        new Color(.55f, .35f, .2f),    // Shotgun  - marrom
        new Color(.3f, .4f, .3f),      // Rifle    - verde escuro
        new Color(.35f, .35f, .4f),    // TMP      - cinza azulado
        new Color(.5f, .25f, .15f),    // Rocket   - vermelho escuro
    };

    public static Vector2Int GetWeaponFootprint(int weaponIdx)
    {
        if (weaponIdx < 0 || weaponIdx >= WeaponSizes.Length)
            return Vector2Int.zero;
        return new Vector2Int(WeaponSizes[weaponIdx][0], WeaponSizes[weaponIdx][1]);
    }

    // ====================================================================
    void Awake()
    {
        I = this;
        grid = new int[gridW, gridH];
        CreateUI();
        // Handgun começa na maleta
        AddWeaponToCase(0);
        casePanel.SetActive(false);
    }

    // ====================================================================
    // UI DA MALETA
    // ====================================================================
    void CreateUI()
    {
        // Canvas próprio (overlay sobre tudo)
        var co = new GameObject("CaseCanvas"); co.transform.SetParent(transform);
        canvas = co.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var sc = co.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        co.AddComponent<GraphicRaycaster>();

        // Painel escuro de fundo
        casePanel = new GameObject("CasePanel"); casePanel.transform.SetParent(canvas.transform, false);
        var panelImg = casePanel.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, .88f);
        var prt = panelImg.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        // Título "ATTACHÉ CASE"
        titleText = MkText(casePanel.transform, "ATTACHÉ CASE", 28, TextAnchor.UpperCenter,
            new Vector2(.5f, 1f), new Vector2(0, -30));
        titleText.color = new Color(.9f, .8f, .5f);
        titleText.fontStyle = FontStyle.Bold;

        // Moldura da maleta (borda marrom couro RE4)
        float totalW = gridW * (CELL + PAD) + PAD + 8;
        float totalH = gridH * (CELL + PAD) + PAD + 8;

        var frame = MkImg(casePanel.transform, "Frame", new Color32(72, 50, 28, 255),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10),
            new Vector2(totalW + 12, totalH + 12));

        // Fundo interno (veludo RE4)
        bgPanel = MkImg(frame.transform, "BG", new Color32(35, 22, 15, 255),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(totalW, totalH));

        // Grid de células
        cellImages = new Image[gridW, gridH];
        float startX = -(totalW / 2f) + PAD + 4 + CELL / 2f;
        float startY = (totalH / 2f) - PAD - 4 - CELL / 2f;

        for (int y = 0; y < gridH; y++)
        {
            for (int x = 0; x < gridW; x++)
            {
                float px = startX + x * (CELL + PAD);
                float py = startY - y * (CELL + PAD);
                cellImages[x, y] = MkImg(bgPanel.transform, $"C{x}_{y}",
                    new Color32(55, 40, 25, 200),
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(px, py), new Vector2(CELL, CELL));
            }
        }

        // Painel de info (lado direito)
        infoPanel = new GameObject("Info"); infoPanel.transform.SetParent(casePanel.transform, false);
        var infoBg = infoPanel.AddComponent<Image>();
        infoBg.color = new Color32(25, 18, 12, 220);
        var irt = infoBg.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(.85f, .5f);
        irt.pivot = new Vector2(.5f, .5f);
        irt.sizeDelta = new Vector2(280, 300);

        infoText = MkText(infoPanel.transform, "", 14, TextAnchor.UpperLeft,
            new Vector2(.5f, .5f), Vector2.zero);
        infoText.color = new Color(.8f, .75f, .6f);
        var infoRt = infoText.rectTransform;
        infoRt.sizeDelta = new Vector2(260, 280);

        // Controles
        controlsText = MkText(casePanel.transform, "[TAB] Close  |  Click: Select  |  R: Rotate  |  T: Auto-sort  |  Arrow Keys: Select",
            13, TextAnchor.LowerCenter, new Vector2(.5f, 0f), new Vector2(0, 20));
        controlsText.color = new Color(.5f, .45f, .35f);

        // Tamanho do grid
        var sizeText = MkText(casePanel.transform, $"Grid: {gridW}×{gridH}", 13,
            TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(20, 20));
        sizeText.color = new Color(.5f, .45f, .35f);
        sizeText.name = "SizeLabel";
        this.sizeText = sizeText;
        UpdateCaseMeta();
    }

    // ====================================================================
    // ABRIR / FECHAR
    // ====================================================================
    public void Toggle()
    {
        isOpen = !isOpen;
        casePanel.SetActive(isOpen);
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            selectedIdx = -1;
            RefreshGrid();
        }
        else
        {
            if (GameManager.I.State == GameManager.GState.Playing)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public bool IsOpen => isOpen;

    // ====================================================================
    // ADICIONAR ARMA À MALETA
    // ====================================================================
    public bool AddWeaponToCase(int weaponIdx)
    {
        // Verifica se já existe
        foreach (var it in items)
            if (it.type == ItemType.Weapon && it.weaponIdx == weaponIdx) return true;

        int w = WeaponSizes[weaponIdx][0], h = WeaponSizes[weaponIdx][1];
        string[] names = { "Handgun", "Shotgun", "Rifle", "TMP", "Rocket Launcher" };

        // Tenta encaixar (tenta normal, depois rotacionado)
        Vector2Int pos = FindSpace(w, h);
        bool rot = false;
        if (pos.x < 0)
        {
            pos = FindSpace(h, w);
            rot = true;
        }
        if (pos.x < 0) return false; // sem espaço!

        var item = new CaseItem
        {
            id = nextId++, name = names[weaponIdx], type = ItemType.Weapon,
            w = w, h = h, gx = pos.x, gy = pos.y, rotated = rot,
            weaponIdx = weaponIdx, quantity = 1,
            color = WeaponColors[weaponIdx]
        };
        items.Add(item);
        PlaceOnGrid(item);
        return true;
    }

    // ====================================================================
    // ADICIONAR CONSUMÍVEL
    // ====================================================================
    public bool AddConsumable(ItemType type, int qty = 1)
    {
        // Stacka com existente
        foreach (var it in items)
        {
            if (it.type == type)
            {
                it.quantity += qty;
                return true;
            }
        }

        int w = 1, h = 1;
        string name = "Item";
        Color col = Color.gray;
        switch (type)
        {
            case ItemType.Herb:    name = "Green Herb"; col = new Color(.2f, .6f, .15f); w = 1; h = 2; break;
            case ItemType.Grenade: name = "Hand Grenade"; col = new Color(.35f, .4f, .25f); w = 1; h = 1; break;
            case ItemType.Ammo:    name = "Ammunition"; col = new Color(.6f, .55f, .2f); w = 2; h = 1; break;
        }

        var pos = FindSpace(w, h);
        bool rot = false;
        if (pos.x < 0) { pos = FindSpace(h, w); rot = true; }
        if (pos.x < 0) return false;

        var item = new CaseItem
        {
            id = nextId++, name = name, type = type,
            w = w, h = h, gx = pos.x, gy = pos.y, rotated = rot,
            weaponIdx = -1, quantity = qty, color = col
        };
        items.Add(item);
        PlaceOnGrid(item);
        return true;
    }

    // ====================================================================
    // GRID LOGIC
    // ====================================================================
    Vector2Int FindSpace(int w, int h)
    {
        for (int y = 0; y <= gridH - h; y++)
            for (int x = 0; x <= gridW - w; x++)
                if (CanPlace(x, y, w, h, 0))
                    return new Vector2Int(x, y);
        return new Vector2Int(-1, -1);
    }

    bool CanPlace(int gx, int gy, int w, int h, int ignoreId)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                int cx = gx + dx, cy = gy + dy;
                if (cx >= gridW || cy >= gridH) return false;
                if (grid[cx, cy] != 0 && grid[cx, cy] != ignoreId) return false;
            }
        return true;
    }

    void PlaceOnGrid(CaseItem item)
    {
        int aw = item.ActualW, ah = item.ActualH;
        for (int dy = 0; dy < ah; dy++)
            for (int dx = 0; dx < aw; dx++)
                grid[item.gx + dx, item.gy + dy] = item.id;
    }

    void ClearFromGrid(CaseItem item)
    {
        for (int y = 0; y < gridH; y++)
            for (int x = 0; x < gridW; x++)
                if (grid[x, y] == item.id) grid[x, y] = 0;
    }

    // ====================================================================
    // ROTACIONAR ITEM SELECIONADO
    // ====================================================================
    void RotateSelected()
    {
        if (selectedIdx < 0 || selectedIdx >= items.Count) return;
        var item = items[selectedIdx];
        ClearFromGrid(item);

        bool newRot = !item.rotated;
        int nw = newRot ? item.h : item.w;
        int nh = newRot ? item.w : item.h;

        // Tenta na mesma posição
        if (CanPlace(item.gx, item.gy, nw, nh, item.id))
        {
            item.rotated = newRot;
            PlaceOnGrid(item);
        }
        else
        {
            // Tenta reposicionar
            var pos = FindSpaceIgnoring(nw, nh, item.id);
            if (pos.x >= 0)
            {
                item.rotated = newRot;
                item.gx = pos.x; item.gy = pos.y;
                PlaceOnGrid(item);
            }
            else
            {
                // Não cabe rotacionado, mantém
                PlaceOnGrid(item);
            }
        }
        RefreshGrid();
    }

    Vector2Int FindSpaceIgnoring(int w, int h, int ignoreId)
    {
        for (int y = 0; y <= gridH - h; y++)
            for (int x = 0; x <= gridW - w; x++)
                if (CanPlace(x, y, w, h, ignoreId))
                    return new Vector2Int(x, y);
        return new Vector2Int(-1, -1);
    }

    // ====================================================================
    // UPGRADE (merchant)
    // ====================================================================
    public bool UpgradeSize()
    {
        if (gridW >= 14 && gridH >= 8) return false; // max
        int newW = Mathf.Min(gridW + 2, 14);
        int newH = Mathf.Min(gridH + 1, 8);

        // Recria grid maior
        int[,] newGrid = new int[newW, newH];
        for (int y = 0; y < gridH && y < newH; y++)
            for (int x = 0; x < gridW && x < newW; x++)
                newGrid[x, y] = grid[x, y];

        grid = newGrid;
        gridW = newW;
        gridH = newH;

        // Recria UI do grid
        Destroy(casePanel);
        CreateUI();
        casePanel.SetActive(isOpen);
        RefreshGrid();
        UpdateCaseMeta();
        return true;
    }

    public bool IsMaxSize => gridW >= 14 && gridH >= 8;

    public void SetSize(int width, int height)
    {
        width = Mathf.Clamp(width, 10, 14);
        height = Mathf.Clamp(height, 6, 8);

        gridW = width;
        gridH = height;
        grid = new int[gridW, gridH];

        if (casePanel) Destroy(casePanel);
        CreateUI();
        casePanel.SetActive(isOpen);
        RefreshGrid();
        UpdateCaseMeta();
    }

    public void ClearAllItems()
    {
        items.Clear();
        nextId = 1;
        selectedIdx = -1;
        if (grid != null) Array.Clear(grid, 0, grid.Length);
        RefreshGrid();
    }

    public void RebuildWeaponsFromPlayer(Player player)
    {
        if (!player || player.weapons == null) return;

        ClearAllItems();
        for (int i = 0; i < player.weapons.Length; i++)
        {
            if (player.weapons[i].owned)
                AddWeaponToCase(i);
        }

        RefreshGrid();
    }

    public bool AutoOrganize()
    {
        if (items.Count <= 1)
        {
            RefreshGrid();
            return true;
        }

        int selectedId = selectedIdx >= 0 && selectedIdx < items.Count ? items[selectedIdx].id : 0;
        var oldOrder = new List<CaseItem>(items);
        int[] oldGX = new int[items.Count];
        int[] oldGY = new int[items.Count];
        bool[] oldRot = new bool[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            oldGX[i] = items[i].gx;
            oldGY[i] = items[i].gy;
            oldRot[i] = items[i].rotated;
        }

        var ordered = new List<CaseItem>(items);
        ordered.Sort(CompareItemsForPacking);
        Array.Clear(grid, 0, grid.Length);

        foreach (var item in ordered)
        {
            if (!TryPlaceAuto(item))
            {
                items = oldOrder;
                Array.Clear(grid, 0, grid.Length);
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].gx = oldGX[i];
                    items[i].gy = oldGY[i];
                    items[i].rotated = oldRot[i];
                    PlaceOnGrid(items[i]);
                }
                selectedIdx = FindItemIndexById(selectedId);
                RefreshGrid();
                return false;
            }
        }

        items = ordered;
        selectedIdx = FindItemIndexById(selectedId);
        if (selectedIdx < 0 && items.Count > 0) selectedIdx = 0;
        RefreshGrid();
        return true;
    }

    // ====================================================================
    // REFRESH VISUAL
    // ====================================================================
    void RefreshGrid()
    {
        if (cellImages == null) return;

        // Reset todas as células
        for (int y = 0; y < gridH; y++)
            for (int x = 0; x < gridW; x++)
                if (x < cellImages.GetLength(0) && y < cellImages.GetLength(1))
                    cellImages[x, y].color = new Color32(55, 40, 25, 200);

        // Pinta itens
        foreach (var item in items)
        {
            int aw = item.ActualW, ah = item.ActualH;
            bool isSel = items.IndexOf(item) == selectedIdx;
            for (int dy = 0; dy < ah; dy++)
            {
                for (int dx = 0; dx < aw; dx++)
                {
                    int cx = item.gx + dx, cy = item.gy + dy;
                    if (cx < gridW && cy < gridH)
                    {
                        Color c = item.color;
                        if (isSel) c = Color.Lerp(c, Color.white, .35f);
                        // Borda do item (escurecer bordas)
                        if (dx == 0 || dy == 0 || dx == aw - 1 || dy == ah - 1)
                            c *= .85f;
                        c.a = 1f;
                        cellImages[cx, cy].color = c;
                    }
                }
            }
        }

        // Info do item selecionado
        UpdateInfoPanel();
        UpdateCaseMeta();
    }

    void UpdateInfoPanel()
    {
        if (!infoPanel) return;
        if (selectedIdx < 0 || selectedIdx >= items.Count)
        {
            infoText.text = "Select an item\nto view details\n\n" +
                $"Items: {items.Count}\n" +
                $"Grid: {gridW} × {gridH}";
            return;
        }

        var item = items[selectedIdx];
        string info = $"<b>{item.name.ToUpper()}</b>\n";
        info += $"Size: {item.w}×{item.h}" + (item.rotated ? " (rotated)" : "") + "\n\n";

        if (item.type == ItemType.Weapon && GameManager.I?.player)
        {
            var w = GameManager.I.player.weapons[item.weaponIdx];
            info += $"Damage: {w.dmg}\n";
            info += $"Fire Rate: {w.fireRate:F2}s\n";
            info += $"Magazine: {w.ammoInMag}/{w.magSize}\n";
            info += $"Reserve: {w.ammoReserve}\n";
            info += $"Reload: {w.reloadTime:F1}s\n";
            info += $"Tune-Up: {GameManager.I.player.GetWeaponLevelSummary(item.weaponIdx)}\n";
            if (w.spread > 0) info += $"Spread: {w.spread:F2}\n";
            if (w.explosive) info += "EXPLOSIVE\n";
            info += $"\nEquipped: {(GameManager.I.player.curWeapon == item.weaponIdx ? "YES" : "NO")}";
        }
        else
        {
            info += $"Quantity: {item.quantity}\n";
            switch (item.type)
            {
                case ItemType.Herb: info += "\nRestores 40 HP"; break;
                case ItemType.Grenade: info += "\nExplosive — Area damage"; break;
                case ItemType.Ammo: info += "\nReloads current weapon"; break;
            }
        }

        infoText.text = info;
    }

    // ====================================================================
    // UPDATE (input quando aberto)
    // ====================================================================
    void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            Toggle();
            return;
        }

        // R = rotacionar
        if (Input.GetKeyDown(KeyCode.R)) RotateSelected();

        // Click = selecionar item
        if (Input.GetMouseButtonDown(0)) HandleClick();

        HandleDirectionalSelection();

        if (Input.GetKeyDown(KeyCode.T))
            AutoOrganize();

        // Número = equipar arma direto
        for (int i = 0; i < 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                // Encontrar arma com esse índice
                for (int j = 0; j < items.Count; j++)
                {
                    if (items[j].type == ItemType.Weapon && items[j].weaponIdx == i)
                    {
                        if (GameManager.I?.player)
                        {
                            GameManager.I.player.SwitchToSlot(i);
                            selectedIdx = j;
                            RefreshGrid();
                        }
                        break;
                    }
                }
            }
        }
    }

    void HandleClick()
    {
        // Converter posição do mouse para célula do grid
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgPanel.rectTransform, Input.mousePosition, null, out mousePos);

        float totalW = gridW * (CELL + PAD) + PAD;
        float totalH = gridH * (CELL + PAD) + PAD;

        // Converter para coordenadas de grid
        float relX = mousePos.x + totalW / 2f;
        float relY = totalH / 2f - mousePos.y;

        int gx = Mathf.FloorToInt(relX / (CELL + PAD));
        int gy = Mathf.FloorToInt(relY / (CELL + PAD));

        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) return;

        int cellId = grid[gx, gy];
        if (cellId == 0)
        {
            // Clicou em vazio — se tem selecionado, tenta mover
            if (selectedIdx >= 0 && selectedIdx < items.Count)
            {
                var item = items[selectedIdx];
                ClearFromGrid(item);
                if (CanPlace(gx, gy, item.ActualW, item.ActualH, item.id))
                {
                    item.gx = gx; item.gy = gy;
                    PlaceOnGrid(item);
                }
                else
                {
                    PlaceOnGrid(item); // mantém posição anterior
                }
            }
            else selectedIdx = -1;
        }
        else
        {
            // Selecionar o item clicado
            for (int i = 0; i < items.Count; i++)
                if (items[i].id == cellId) { selectedIdx = i; break; }

            // Se clicou em arma, equipa
            if (selectedIdx >= 0 && items[selectedIdx].type == ItemType.Weapon && GameManager.I?.player)
            {
                GameManager.I.player.SwitchToSlot(items[selectedIdx].weaponIdx);
            }
        }
        RefreshGrid();
    }

    // ====================================================================
    // QUERIES
    // ====================================================================
    public bool HasWeapon(int weaponIdx)
    {
        foreach (var it in items)
            if (it.type == ItemType.Weapon && it.weaponIdx == weaponIdx) return true;
        return false;
    }

    public bool HasFreeSpace(int w, int h)
    {
        return FindSpace(w, h).x >= 0 || FindSpace(h, w).x >= 0;
    }

    public int CountType(ItemType type)
    {
        foreach (var it in items) if (it.type == type) return it.quantity;
        return 0;
    }

    public bool ConsumeItem(ItemType type, int qty = 1)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].type == type && items[i].quantity >= qty)
            {
                items[i].quantity -= qty;
                if (items[i].quantity <= 0)
                {
                    ClearFromGrid(items[i]);
                    items.RemoveAt(i);
                    if (selectedIdx >= items.Count) selectedIdx = -1;
                }
                RefreshGrid();
                return true;
            }
        }
        return false;
    }

    void HandleDirectionalSelection()
    {
        Vector2Int dir = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector2Int.right;
        else if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector2Int.down;
        else return;

        if (items.Count == 0) return;

        if (selectedIdx < 0 || selectedIdx >= items.Count)
        {
            selectedIdx = 0;
            RefreshGrid();
            return;
        }

        int next = FindAdjacentItem(dir);
        if (next < 0) return;

        selectedIdx = next;
        if (items[selectedIdx].type == ItemType.Weapon && GameManager.I?.player)
            GameManager.I.player.SwitchToSlot(items[selectedIdx].weaponIdx);
        RefreshGrid();
    }

    int FindAdjacentItem(Vector2Int dir)
    {
        if (selectedIdx < 0 || selectedIdx >= items.Count) return -1;

        Vector2 currentCenter = GetItemCenter(items[selectedIdx]);
        float bestScore = float.MaxValue;
        int bestIdx = -1;

        for (int i = 0; i < items.Count; i++)
        {
            if (i == selectedIdx) continue;

            Vector2 delta = GetItemCenter(items[i]) - currentCenter;
            if (dir.x < 0 && delta.x >= -0.05f) continue;
            if (dir.x > 0 && delta.x <= 0.05f) continue;
            if (dir.y < 0 && delta.y >= -0.05f) continue;
            if (dir.y > 0 && delta.y <= 0.05f) continue;

            float primary = dir.x != 0 ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
            float secondary = dir.x != 0 ? Mathf.Abs(delta.y) : Mathf.Abs(delta.x);
            float score = primary * 5f + secondary;

            if (score < bestScore)
            {
                bestScore = score;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    Vector2 GetItemCenter(CaseItem item)
    {
        return new Vector2(item.gx + item.ActualW * .5f, -(item.gy + item.ActualH * .5f));
    }

    int FindItemIndexById(int itemId)
    {
        if (itemId == 0) return -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i].id == itemId) return i;
        return -1;
    }

    int CompareItemsForPacking(CaseItem a, CaseItem b)
    {
        int typeCompare = GetPackingTypeRank(a.type).CompareTo(GetPackingTypeRank(b.type));
        if (typeCompare != 0) return typeCompare;

        int areaA = a.ActualW * a.ActualH;
        int areaB = b.ActualW * b.ActualH;
        int areaCompare = areaB.CompareTo(areaA);
        if (areaCompare != 0) return areaCompare;

        int widthCompare = b.ActualW.CompareTo(a.ActualW);
        if (widthCompare != 0) return widthCompare;

        return string.Compare(a.name, b.name, StringComparison.Ordinal);
    }

    int GetPackingTypeRank(ItemType type)
    {
        switch (type)
        {
            case ItemType.Weapon: return 0;
            case ItemType.Ammo: return 1;
            case ItemType.Herb: return 2;
            case ItemType.Grenade: return 3;
            default: return 4;
        }
    }

    bool TryPlaceAuto(CaseItem item)
    {
        bool originalRotation = item.rotated;
        Vector2Int pos = FindSpace(item.ActualW, item.ActualH);
        if (pos.x >= 0)
        {
            item.gx = pos.x;
            item.gy = pos.y;
            PlaceOnGrid(item);
            return true;
        }

        if (item.w == item.h) return false;

        item.rotated = !item.rotated;
        pos = FindSpace(item.ActualW, item.ActualH);
        if (pos.x >= 0)
        {
            item.gx = pos.x;
            item.gy = pos.y;
            PlaceOnGrid(item);
            return true;
        }

        item.rotated = originalRotation;
        return false;
    }

    void UpdateCaseMeta()
    {
        if (sizeText)
            sizeText.text = $"Grid: {gridW}x{gridH}  |  Items: {items.Count}";
    }

    // ====================================================================
    // UI HELPERS
    // ====================================================================
    Image MkImg(Transform par, string n, Color c, Vector2 anc, Vector2 piv, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(n); go.transform.SetParent(par, false);
        var img = go.AddComponent<Image>(); img.color = c;
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anc; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        return img;
    }

    Text MkText(Transform par, string txt, int sz, TextAnchor al, Vector2 anc, Vector2 pos)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(par, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!t.font) t.font = Font.CreateDynamicFontFromOSFont("Arial", sz);
        t.text = txt; t.fontSize = sz; t.alignment = al; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anc;
        rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(500, 80);
        return t;
    }
}
