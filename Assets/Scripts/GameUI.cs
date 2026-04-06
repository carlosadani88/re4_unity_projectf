// ============================================================================
// VILL4GE — GameUI.cs  (RE4-faithful)
// HUD estilo RE4: barra HP estilo ECG, munição canto inferior direito,
// wave intro "WAVE X", merchant "What are ya buyin'?", morte estilo RE4.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI I;

    Canvas canvas;
    // HUD
    Image hpFill, hpFrame, dmgFlash, dmgVignette;
    Text ammoText, reserveText, waveText, moneyText, weaponText, infoText, killsText;
    // Overlays
    GameObject titlePanel, deathPanel, merchantPanel, waveIntroPanel;
    Text waveIntroText;
    bool merchantOpen;
    // Kick prompt
    Text kickPromptText;
    float kickPromptAlpha;

    void Awake()
    {
        I = this;
        CreateCanvas();
        CreateHUD();
        CreateCrosshair();
        CreateKickPrompt();
        CreateTitleScreen();
        CreateDeathScreen();
        CreateMerchantPanel();
        CreateWaveIntro();
        CreateDamageEffects();
    }

    void CreateCanvas()
    {
        var co = new GameObject("Canvas"); co.transform.SetParent(transform);
        canvas = co.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = co.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
        co.AddComponent<GraphicRaycaster>();
        var es = new GameObject("ES"); es.transform.SetParent(transform);
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ====================================================================
    // HUD (estilo RE4)
    // ====================================================================
    void CreateHUD()
    {
        // ── HP (bottom-left, estilo RE4 com moldura) ──
        hpFrame = Img(canvas.transform, "HPFrame", new Color32(20, 20, 20, 180),
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 40), new Vector2(240, 28));
        hpFill = Img(hpFrame.transform, "HPFill", new Color32(180, 25, 25, 255),
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(3, 3), new Vector2(234, 22));
        // Label "LIFE"
        var lifeLabel = Txt(hpFrame.transform, "LifeL", 11, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(5, 6));
        lifeLabel.text = "LIFE"; lifeLabel.color = new Color(.7f, .7f, .7f);

        // ── Ammo (bottom-right, estilo RE4) ──
        ammoText = Txt(canvas.transform, "Ammo", 28, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-25, 35));
        ammoText.fontStyle = FontStyle.Bold;
        reserveText = Txt(canvas.transform, "Reserve", 14, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-25, 15));
        reserveText.color = new Color(.6f, .6f, .6f);

        // ── Weapon name ──
        weaponText = Txt(canvas.transform, "Wpn", 16, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-25, 68));
        weaponText.color = new Color(.85f, .75f, .5f);

        // ── Wave/Enemies (top-right) ──
        waveText = Txt(canvas.transform, "Wave", 18, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-20, -15));
        waveText.fontStyle = FontStyle.Bold;
        killsText = Txt(canvas.transform, "Kills", 14, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-20, -40));
        killsText.color = new Color(.7f, .7f, .7f);

        // ── Money + Items (bottom-left below HP) ──
        moneyText = Txt(canvas.transform, "Money", 15, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(20, 10));
        moneyText.color = new Color(.9f, .82f, .45f);

        // ── Info contextual ──
        infoText = Txt(canvas.transform, "Info", 16, TextAnchor.LowerCenter, new Vector2(.5f, 0), new Vector2(0, 50));
        infoText.color = new Color(1, .85f, .4f);
    }

    // ====================================================================
    // CROSSHAIR (dot style RE4 quando não está mirando)
    // ====================================================================
    void CreateCrosshair()
    {
        // Pequeno ponto central (aparece sem mirar)
        Img(canvas.transform, "Dot", new Color(1, 1, 1, .4f),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(3, 3));
    }

    // ====================================================================
    // KICK PROMPT (RE4 context-sensitive)
    // ====================================================================
    void CreateKickPrompt()
    {
        kickPromptText = Txt(canvas.transform, "Kick", 22, TextAnchor.MiddleCenter,
            new Vector2(.5f, .35f), Vector2.zero);
        kickPromptText.text = "[F] KICK";
        kickPromptText.color = new Color(1, .9f, .4f, 0);
        kickPromptText.fontStyle = FontStyle.Bold;

        // Fundo sutil atrás do prompt
        var bg = Img(kickPromptText.rectTransform, "KBG", new Color(0, 0, 0, 0),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(140, 36));
        bg.raycastTarget = false;
        bg.transform.SetAsFirstSibling();
    }

    public void ShowKickPrompt(bool show)
    {
        float target = show ? 1f : 0f;
        kickPromptAlpha = Mathf.Lerp(kickPromptAlpha, target, Time.deltaTime * 12);
        if (kickPromptText)
        {
            kickPromptText.color = new Color(1, .9f, .4f, kickPromptAlpha);
            // Pulsar texto quando visível
            if (show)
            {
                float pulse = .85f + Mathf.Sin(Time.time * 6) * .15f;
                kickPromptText.transform.localScale = Vector3.one * pulse;
            }
            else
                kickPromptText.transform.localScale = Vector3.one;
        }
    }

    // ====================================================================
    // TELA TÍTULO
    // ====================================================================
    void CreateTitleScreen()
    {
        titlePanel = Panel(canvas.transform, "Title", new Color(0, 0, 0, .92f));
        var t1 = Txt(titlePanel.transform, "T1", 80, TextAnchor.MiddleCenter, new Vector2(.5f, .6f), Vector2.zero);
        t1.text = "VILL4GE"; t1.color = new Color(.85f, .12f, .08f); t1.fontStyle = FontStyle.Bold;
        var t1b = Txt(titlePanel.transform, "T1b", 18, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero);
        t1b.text = "A Resident Evil 4 Tribute"; t1b.color = new Color(.5f, .4f, .3f); t1b.fontStyle = FontStyle.Italic;
        var t2 = Txt(titlePanel.transform, "T2", 20, TextAnchor.MiddleCenter, new Vector2(.5f, .32f), Vector2.zero);
        t2.text = "PRESS ANY KEY"; t2.color = new Color(.6f, .55f, .45f);
        // Controles
        var t3 = Txt(titlePanel.transform, "T3", 13, TextAnchor.MiddleCenter, new Vector2(.5f, .18f), Vector2.zero);
        t3.text = "WASD Move  |  RMB Aim  |  LMB Shoot/Knife  |  F Kick  |  TAB Case  |  R Reload  |  G Grenade  |  H Herb  |  E Interact";
        t3.color = new Color(.4f, .4f, .4f);
    }

    // ====================================================================
    // TELA DE MORTE (RE4 "YOU ARE DEAD")
    // ====================================================================
    void CreateDeathScreen()
    {
        deathPanel = Panel(canvas.transform, "Death", new Color(.2f, 0, 0, .92f));
        var t1 = Txt(deathPanel.transform, "D1", 72, TextAnchor.MiddleCenter, new Vector2(.5f, .58f), Vector2.zero);
        t1.text = "YOU ARE DEAD"; t1.color = new Color(.9f, .15f, .1f); t1.fontStyle = FontStyle.Bold;
        var t2 = Txt(deathPanel.transform, "D2", 20, TextAnchor.MiddleCenter, new Vector2(.5f, .42f), Vector2.zero);
        t2.text = "Press R to Continue"; t2.color = new Color(.5f, .4f, .35f);
        deathPanel.SetActive(false);
    }

    // ====================================================================
    // WAVE INTRO
    // ====================================================================
    void CreateWaveIntro()
    {
        waveIntroPanel = Panel(canvas.transform, "WaveIntro", new Color(0, 0, 0, .6f));
        waveIntroText = Txt(waveIntroPanel.transform, "WI", 48, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero);
        waveIntroText.color = new Color(.9f, .8f, .5f); waveIntroText.fontStyle = FontStyle.Bold;
        waveIntroPanel.SetActive(false);
    }

    // ====================================================================
    // MERCHANT PANEL ("What are ya buyin'?")
    // ====================================================================
    void CreateMerchantPanel()
    {
        merchantPanel = Panel(canvas.transform, "Merchant", new Color(0, 0, 0, .94f));

        var title = Txt(merchantPanel.transform, "MT", 32, TextAnchor.UpperCenter, new Vector2(.5f, .92f), Vector2.zero);
        title.text = "Welcome, Stranger."; title.color = new Color(.85f, .7f, .3f); title.fontStyle = FontStyle.Italic;
        var sub = Txt(merchantPanel.transform, "MS", 18, TextAnchor.UpperCenter, new Vector2(.5f, .86f), Vector2.zero);
        sub.text = "What are ya buyin'?"; sub.color = new Color(.6f, .5f, .3f);

        string[] labels = { "First Aid Herb  $200", "Ammunition  $100", "Shotgun  $2000",
                            "Rifle  $3000", "TMP  $1500", "Rocket Launcher  $5000",
                            "Firepower +20%  $1000", "Grenades x3  $500",
                            "Case Upgrade  $3000" };
        int[] costs = { 200, 100, 2000, 3000, 1500, 5000, 1000, 500, 3000 };

        for (int i = 0; i < labels.Length; i++)
        {
            float y = .76f - i * .072f;
            var btn = MakeBtn(merchantPanel.transform, labels[i], new Vector2(.5f, y), new Vector2(380, 36));
            int idx = i; int cost = costs[i];
            btn.onClick.AddListener(() => Buy(idx, cost));
        }

        var cont = MakeBtn(merchantPanel.transform, "▶  NEXT WAVE", new Vector2(.5f, .08f), new Vector2(280, 44));
        cont.onClick.AddListener(OnContinue);
        merchantPanel.SetActive(false);
    }

    // ====================================================================
    // DAMAGE EFFECTS
    // ====================================================================
    void CreateDamageEffects()
    {
        // Flash vermelho
        dmgFlash = Img(canvas.transform, "DFlash", new Color(.7f, 0, 0, 0),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(2200, 1400));
        dmgFlash.raycastTarget = false;
        // Vignette (bordas escuras permanentes RE4-style)
        dmgVignette = Img(canvas.transform, "Vig", new Color(0, 0, 0, .25f),
            new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(2200, 1400));
        dmgVignette.raycastTarget = false;
    }

    float dmgTimer;
    public void FlashDamage() => dmgTimer = .25f;

    // ====================================================================
    // UPDATE
    // ====================================================================
    void Update()
    {
        if (!GameManager.I) return;
        var gm = GameManager.I;
        var p = gm.player;

        titlePanel.SetActive(gm.State == GameManager.GState.Title);
        deathPanel.SetActive(gm.State == GameManager.GState.Dead);
        waveIntroPanel.SetActive(gm.State == GameManager.GState.WaveIntro);
        if (gm.State == GameManager.GState.WaveIntro)
            waveIntroText.text = $"— WAVE {gm.wave} —";

        if (!p) return;

        // HP
        float pct = p.hp / p.maxHp;
        var rt = hpFill.rectTransform;
        rt.sizeDelta = new Vector2(234 * pct, 22);
        hpFill.color = pct > .5f ? new Color32(180, 25, 25, 255) :
                       pct > .25f ? new Color32(200, 100, 20, 255) :
                       Color.Lerp(new Color32(200, 20, 20, 255), new Color32(100, 10, 10, 255), Mathf.Sin(Time.time * 8) * .5f + .5f);

        // Vignette intensifica com dano
        float vigAlpha = Mathf.Lerp(.25f, .6f, 1 - pct);
        dmgVignette.color = new Color(pct < .3f ? .15f : 0, 0, 0, vigAlpha);

        // Arma + Munição
        var w = p.weapons[p.curWeapon];
        weaponText.text = w.name.ToUpper();
        ammoText.text = $"{w.ammoInMag}";
        reserveText.text = $"/ {w.magSize}   Reserve: {w.ammoReserve}";

        waveText.text = $"Wave {gm.wave}   Enemies: {gm.enemiesAlive}";
        killsText.text = $"Kills: {gm.kills}";

        moneyText.text = $"₧ {p.money}   Herbs: {p.herbs}   Grenades: {p.grenades}";

        // Indicador maleta (TAB)
        if (GameManager.I.State == GameManager.GState.Playing && !AttacheCase.I?.IsOpen == true)
        {
            if (string.IsNullOrEmpty(infoText.text))
                infoText.text = "";
        }

        // Info contextual
        if (gm.State == GameManager.GState.Merchant &&
            Vector3.Distance(p.transform.position, gm.merchantPos) < 4f)
            infoText.text = "[E] Talk to Merchant";
        else if (gm.State == GameManager.GState.Playing)
            infoText.text = "";
        else
            infoText.text = "";

        // Flash dano
        if (dmgTimer > 0) { dmgTimer -= Time.deltaTime; dmgFlash.color = new Color(.7f, 0, 0, dmgTimer * 2); }
        else dmgFlash.color = Color.clear;
    }

    // ====================================================================
    // MERCHANT LOGIC
    // ====================================================================
    public void ToggleMerchant()
    {
        merchantOpen = !merchantOpen;
        merchantPanel.SetActive(merchantOpen);
    }

    void Buy(int idx, int cost)
    {
        var p = GameManager.I?.player;
        if (!p || p.money < cost) return;
        switch (idx)
        {
            case 0: p.herbs++; break;
            case 1: var w = p.weapons[p.curWeapon]; w.ammoReserve += w.magSize * 2; break;
            case 2:
                if (p.weapons[1].owned) return;
                if (AttacheCase.I && !AttacheCase.I.AddWeaponToCase(1)) return; // sem espaço
                p.weapons[1].owned = true; break;
            case 3:
                if (p.weapons[2].owned) return;
                if (AttacheCase.I && !AttacheCase.I.AddWeaponToCase(2)) return;
                p.weapons[2].owned = true; break;
            case 4:
                if (p.weapons[3].owned) return;
                if (AttacheCase.I && !AttacheCase.I.AddWeaponToCase(3)) return;
                p.weapons[3].owned = true; break;
            case 5:
                if (p.weapons[4].owned) return;
                if (AttacheCase.I && !AttacheCase.I.AddWeaponToCase(4)) return;
                p.weapons[4].owned = true; break;
            case 6: p.dmgMultiplier += .2f; break;
            case 7: p.grenades += 3; break;
            case 8: // Case upgrade
                if (AttacheCase.I && !AttacheCase.I.IsMaxSize) AttacheCase.I.UpgradeSize();
                else return;
                break;
        }
        p.money -= cost;
    }

    void OnContinue()
    {
        merchantOpen = false; merchantPanel.SetActive(false);
        GameManager.I?.CloseMerchant();
    }

    // ====================================================================
    // UI HELPERS
    // ====================================================================
    Image Img(Transform par, string n, Color c, Vector2 anc, Vector2 piv, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(n); go.transform.SetParent(par, false);
        var img = go.AddComponent<Image>(); img.color = c;
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anc; rt.pivot = piv; rt.anchoredPosition = pos; rt.sizeDelta = sz;
        return img;
    }
    Text Txt(Transform par, string n, int sz, TextAnchor al, Vector2 anc, Vector2 pos)
    {
        var go = new GameObject(n); go.transform.SetParent(par, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!t.font) t.font = Font.CreateDynamicFontFromOSFont("Arial", sz);
        t.fontSize = sz; t.alignment = al; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anc; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(500, 80);
        return t;
    }
    GameObject Panel(Transform par, string n, Color c)
    {
        var go = new GameObject(n); go.transform.SetParent(par, false);
        var img = go.AddComponent<Image>(); img.color = c;
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }
    Button MakeBtn(Transform par, string label, Vector2 anc, Vector2 sz)
    {
        var go = new GameObject("Btn"); go.transform.SetParent(par, false);
        var img = go.AddComponent<Image>(); img.color = new Color32(45, 38, 28, 220);
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anc; rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = sz;
        var btn = go.AddComponent<Button>();
        var cb = btn.colors; cb.highlightedColor = new Color32(90, 72, 45, 255); cb.pressedColor = new Color32(130, 105, 55, 255); btn.colors = cb;
        var tgo = new GameObject("L"); tgo.transform.SetParent(go.transform, false);
        var txt = tgo.AddComponent<Text>(); txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!txt.font) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 15);
        txt.fontSize = 15; txt.alignment = TextAnchor.MiddleCenter; txt.color = new Color(.9f, .85f, .7f);
        var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }
}
