// ============================================================================
// VILL4GE — GameUI.cs  (RE4-faithful)
// HUD estilo RE4: barra HP estilo ECG, munição canto inferior direito,
// wave intro "WAVE X", merchant "What are ya buyin'?", morte estilo RE4.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameUI : MonoBehaviour
{
    public static GameUI I;

    enum MerchantOfferType
    {
        GreenHerb,
        RedHerb,
        YellowHerb,
        Ammo,
        HandGrenade,
        FlashGrenade,
        IncendiaryGrenade,
        PunisherReward,
        SellTreasures,
        Recovery,
        WeaponUnlock,
        UpgradeFirepower,
        UpgradeReload,
        UpgradeCapacity,
        UpgradeExclusive,
        CaseUpgrade
    }

    class MerchantOffer
    {
        public MerchantOfferType type;
        public int weaponIdx = -1;
        public Player.UpgradeCategory upgradeCategory;
        public string label;
        public string detail;
        public string unavailableReason;
        public int price;
        public bool grantsMoney;
        public bool available;
    }

    Canvas canvas;
    // HUD
    Image hpFill, hpFrame, dmgFlash, dmgVignette;
    Text ammoText, reserveText, waveText, moneyText, weaponText, infoText, killsText, objectiveText, statusText, pickupText;
    // Overlays
    GameObject titlePanel, deathPanel, merchantPanel, waveIntroPanel;
    Text waveIntroText, merchantSummaryText, merchantWeaponText, merchantMetaText;
    Button[] merchantButtons;
    Text[] merchantButtonLabels;
    MerchantOffer[] merchantOffers;
    bool merchantOpen;
    // Kick prompt
    Text kickPromptText;
    float kickPromptAlpha;
    float statusTimer, statusDuration, pickupTimer, pickupDuration;
    Color statusColor = Color.white, pickupColor = Color.white;

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

        objectiveText = Txt(canvas.transform, "Objective", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(20, -15));
        objectiveText.color = new Color(.86f, .8f, .68f);
        objectiveText.rectTransform.sizeDelta = new Vector2(760, 60);

        statusText = Txt(canvas.transform, "Status", 24, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(0, -32));
        statusText.fontStyle = FontStyle.Bold;
        statusText.color = new Color(1, 1, 1, 0);
        statusText.rectTransform.sizeDelta = new Vector2(900, 80);

        pickupText = Txt(canvas.transform, "Pickup", 18, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(0, -72));
        pickupText.color = new Color(1, 1, 1, 0);
        pickupText.rectTransform.sizeDelta = new Vector2(900, 80);

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

    public void ShowKickPrompt(bool show, string label = "KICK")
    {
        float target = show ? 1f : 0f;
        kickPromptAlpha = Mathf.Lerp(kickPromptAlpha, target, Time.deltaTime * 12);
        if (kickPromptText)
        {
            kickPromptText.text = "[F] " + label;
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
        t3.text = "WASD Move  |  RMB Aim  |  LMB Shoot/Knife  |  F Context Melee  |  TAB Case  |  T Auto-sort  |  R Reload  |  B Cycle Grenade  |  G Throw  |  H Herbs  |  E Interact";
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
        sub.text = "What are ya buyin'? Tune-up, restock and get ready."; sub.color = new Color(.6f, .5f, .3f);

        Img(merchantPanel.transform, "OffersBG", new Color32(20, 16, 12, 220),
            new Vector2(.28f, .49f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(610, 720));
        Img(merchantPanel.transform, "InfoBG", new Color32(24, 19, 14, 220),
            new Vector2(.73f, .49f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(500, 720));

        merchantButtons = new Button[15];
        merchantButtonLabels = new Text[15];
        merchantOffers = new MerchantOffer[15];

        for (int i = 0; i < merchantButtons.Length; i++)
        {
            float y = .79f - i * .047f;
            var btn = MakeBtn(merchantPanel.transform, "", new Vector2(.28f, y), new Vector2(560, 40));
            int idx = i;
            btn.onClick.AddListener(() => Buy(idx));
            merchantButtons[i] = btn;
            merchantButtonLabels[i] = btn.GetComponentInChildren<Text>();
            merchantButtonLabels[i].alignment = TextAnchor.MiddleLeft;
            merchantButtonLabels[i].fontSize = 13;
            merchantButtonLabels[i].rectTransform.offsetMin = new Vector2(14, 0);
            merchantButtonLabels[i].rectTransform.offsetMax = new Vector2(-14, 0);
        }

        merchantSummaryText = Txt(merchantPanel.transform, "MInfo", 18, TextAnchor.UpperLeft, new Vector2(.73f, .78f), Vector2.zero);
        merchantSummaryText.rectTransform.sizeDelta = new Vector2(440, 220);
        merchantSummaryText.color = new Color(.9f, .85f, .72f);

        merchantWeaponText = Txt(merchantPanel.transform, "MWeapon", 16, TextAnchor.UpperLeft, new Vector2(.73f, .54f), Vector2.zero);
        merchantWeaponText.rectTransform.sizeDelta = new Vector2(440, 250);
        merchantWeaponText.color = new Color(.75f, .72f, .65f);

        merchantMetaText = Txt(merchantPanel.transform, "MMeta", 14, TextAnchor.UpperLeft, new Vector2(.73f, .24f), Vector2.zero);
        merchantMetaText.rectTransform.sizeDelta = new Vector2(440, 180);
        merchantMetaText.color = new Color(.72f, .64f, .5f);

        var cont = MakeBtn(merchantPanel.transform, "NEXT WAVE", new Vector2(.73f, .08f), new Vector2(300, 46));
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
        if (gm.State != GameManager.GState.Merchant && merchantOpen)
        {
            merchantOpen = false;
            merchantPanel.SetActive(false);
        }
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

        objectiveText.text = BuildObjectiveText(gm);
        infoText.text = BuildHintText(gm, p, w, pct);
        moneyText.text = $"{p.money} PTAS   Herbs G/R/Y: {p.herbs}/{p.redHerbs}/{p.yellowHerbs}   " +
            $"Grenades H/F/I: {p.grenades}/{p.flashGrenades}/{p.incendiaryGrenades}   " +
            $"Selected: {p.GetSelectedGrenadeLabel()}" +
            (p.GetTreasureCount() > 0 ? $"   Treasure: {p.GetTreasureCount()}" : "");

        if (merchantOpen)
        {
            RefreshMerchantPanel();
            if (Input.GetKeyDown(KeyCode.Escape))
                ToggleMerchant();
        }

        // Flash dano
        if (dmgTimer > 0) { dmgTimer -= Time.deltaTime; dmgFlash.color = new Color(.7f, 0, 0, dmgTimer * 2); }
        else dmgFlash.color = Color.clear;
        UpdateTransientLabel(statusText, ref statusTimer, statusDuration, statusColor);
        UpdateTransientLabel(pickupText, ref pickupTimer, pickupDuration, pickupColor);
    }

    // ====================================================================
    // MERCHANT LOGIC
    // ====================================================================
    public void ToggleMerchant()
    {
        if (!GameManager.I || GameManager.I.State != GameManager.GState.Merchant) return;
        merchantOpen = !merchantOpen;
        merchantPanel.SetActive(merchantOpen);
        if (merchantOpen) RefreshMerchantPanel();
        if (AudioManager.I) AudioManager.I.PlaySFX(merchantOpen ? AudioManager.SFX.UIOpen : AudioManager.SFX.UIClose);
    }

    void BuyLegacy(int idx, int cost)
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
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.MerchantBuy);
        ShowStatusMessage("PURCHASE COMPLETE", new Color32(215, 185, 90, 255), 1.4f);
    }

    void OnContinue()
    {
        merchantOpen = false; merchantPanel.SetActive(false);
        GameManager.I?.CloseMerchant();
    }

    void Buy(int idx)
    {
        var gm = GameManager.I;
        var p = gm?.player;
        if (!gm || !p) return;

        RefreshMerchantPanel();
        if (merchantOffers == null || idx < 0 || idx >= merchantOffers.Length) return;

        MerchantOffer offer = merchantOffers[idx];
        if (!offer.available)
        {
            ShowStatusMessage(string.IsNullOrEmpty(offer.unavailableReason) ? "CAN'T BUY THAT" : offer.unavailableReason.ToUpper(),
                new Color32(220, 90, 80, 255), 1.4f);
            return;
        }

        if (!offer.grantsMoney && p.money < offer.price)
        {
            ShowStatusMessage("NOT ENOUGH PTAS.", new Color32(220, 90, 80, 255), 1.4f);
            return;
        }

        bool success = false;
        string successMessage = "PURCHASE COMPLETE";

        switch (offer.type)
        {
            case MerchantOfferType.GreenHerb:
                p.herbs++;
                success = true;
                successMessage = "GREEN HERB STOCKED";
                break;
            case MerchantOfferType.RedHerb:
                p.redHerbs++;
                success = true;
                successMessage = "RED HERB STOCKED";
                break;
            case MerchantOfferType.YellowHerb:
                p.yellowHerbs++;
                success = true;
                successMessage = "YELLOW HERB STOCKED";
                break;
            case MerchantOfferType.Ammo:
                p.weapons[p.curWeapon].ammoReserve += GetAmmoBundleAmount(p.curWeapon);
                success = true;
                successMessage = p.weapons[p.curWeapon].name.ToUpper() + " AMMO STOCKED";
                break;
            case MerchantOfferType.HandGrenade:
                p.grenades += 2;
                success = true;
                successMessage = "HAND GRENADES STOCKED";
                break;
            case MerchantOfferType.FlashGrenade:
                p.flashGrenades += 1;
                p.ValidateGrenadeSelection();
                success = true;
                successMessage = "FLASH GRENADE STOCKED";
                break;
            case MerchantOfferType.IncendiaryGrenade:
                p.incendiaryGrenades += 1;
                p.ValidateGrenadeSelection();
                success = true;
                successMessage = "INCENDIARY GRENADE STOCKED";
                break;
            case MerchantOfferType.PunisherReward:
                if (gm.ClaimPunisherReward())
                {
                    success = true;
                    successMessage = p.punisherSpecialUnlocked ? "PUNISHER SPECIAL CLAIMED" : "PUNISHER CLAIMED";
                }
                break;
            case MerchantOfferType.SellTreasures:
                int saleValue = p.SellAllTreasures();
                if (saleValue > 0)
                {
                    success = true;
                    successMessage = "TREASURES SOLD +" + saleValue;
                }
                break;
            case MerchantOfferType.Recovery:
                p.hp = p.maxHp;
                success = true;
                successMessage = "HEALTH FULLY RESTORED";
                break;
            case MerchantOfferType.WeaponUnlock:
                if (offer.weaponIdx >= 0 && offer.weaponIdx < p.weapons.Length)
                {
                    if (AttacheCase.I && !AttacheCase.I.AddWeaponToCase(offer.weaponIdx))
                    {
                        ShowStatusMessage("NO ROOM IN THE CASE", new Color32(220, 90, 80, 255), 1.4f);
                        return;
                    }
                    p.weapons[offer.weaponIdx].owned = true;
                    p.SwitchToSlot(offer.weaponIdx);
                    success = true;
                    successMessage = p.weapons[offer.weaponIdx].name.ToUpper() + " ACQUIRED";
                }
                break;
            case MerchantOfferType.UpgradeFirepower:
            case MerchantOfferType.UpgradeReload:
            case MerchantOfferType.UpgradeCapacity:
                if (p.UpgradeWeapon(p.curWeapon, offer.upgradeCategory))
                {
                    success = true;
                    successMessage = p.weapons[p.curWeapon].name.ToUpper() + " TUNE-UP COMPLETE";
                }
                break;
            case MerchantOfferType.UpgradeExclusive:
                if (p.UpgradeExclusive(p.curWeapon))
                {
                    success = true;
                    successMessage = p.weapons[p.curWeapon].name.ToUpper() + " EXCLUSIVE TUNE-UP COMPLETE";
                }
                break;
            case MerchantOfferType.CaseUpgrade:
                if (AttacheCase.I && AttacheCase.I.UpgradeSize())
                {
                    AttacheCase.I.AutoOrganize();
                    success = true;
                    successMessage = "ATTACHE CASE EXPANDED";
                }
                break;
        }

        if (!success) return;

        if (offer.grantsMoney)
        {
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.MerchantSell);
        }
        else
        {
            p.money -= offer.price;
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.MerchantBuy);
        }
        ShowStatusMessage(successMessage, new Color32(215, 185, 90, 255), 1.4f);
        RefreshMerchantPanel();
    }

    void RefreshMerchantPanel()
    {
        var gm = GameManager.I;
        var p = gm?.player;
        if (!merchantPanel || !gm || !p || merchantButtons == null) return;

        merchantOffers = BuildMerchantOffers(gm, p);
        for (int i = 0; i < merchantButtons.Length && i < merchantOffers.Length; i++)
        {
            MerchantOffer offer = merchantOffers[i];
            merchantButtons[i].interactable = offer.available;
            if (merchantButtons[i].image)
                merchantButtons[i].image.color = offer.available ? new Color32(45, 38, 28, 220) : new Color32(28, 24, 20, 215);
            if (merchantButtonLabels[i])
            {
                merchantButtonLabels[i].text = BuildMerchantOfferLabel(offer);
                merchantButtonLabels[i].color = offer.available ? new Color(.92f, .86f, .72f) : new Color(.48f, .45f, .4f);
            }
        }

        if (merchantSummaryText) merchantSummaryText.text = BuildMerchantSummary(gm, p);
        if (merchantWeaponText) merchantWeaponText.text = BuildMerchantWeaponText(p);
        if (merchantMetaText) merchantMetaText.text = BuildMerchantMetaText(gm, p);
    }

    MerchantOffer[] BuildMerchantOffers(GameManager gm, Player p)
    {
        var offers = new MerchantOffer[15];
        int nextWeapon = FindNextWeaponUnlock(p);
        int caseBasePrice = GetCaseUpgradeBasePrice();
        int ammoBasePrice = GetAmmoBundleBasePrice(p.curWeapon);
        int ammoAmount = GetAmmoBundleAmount(p.curWeapon);
        int treasureValue = p.GetTreasureSellValue();

        offers[0] = new MerchantOffer
        {
            type = MerchantOfferType.GreenHerb,
            label = "Green Herb",
            detail = "Emergency healing stock.",
            price = gm.GetMerchantPrice(200),
            available = true
        };

        offers[1] = new MerchantOffer
        {
            type = MerchantOfferType.RedHerb,
            label = "Red Herb",
            detail = "Pairs with a green herb for full recovery.",
            price = gm.GetMerchantPrice(320),
            available = true
        };

        offers[2] = new MerchantOffer
        {
            type = MerchantOfferType.YellowHerb,
            label = "Yellow Herb",
            detail = "Boosts max life when mixed with green.",
            price = gm.GetMerchantPrice(760),
            available = p.maxHp < 160f || p.herbs > 0,
            unavailableReason = "Health already maxed"
        };

        offers[3] = new MerchantOffer
        {
            type = MerchantOfferType.Ammo,
            label = p.weapons[p.curWeapon].name + " Ammo +" + ammoAmount,
            detail = "Reserve ammo for the equipped weapon.",
            price = gm.GetMerchantPrice(ammoBasePrice),
            available = ammoAmount > 0
        };

        offers[4] = new MerchantOffer
        {
            type = MerchantOfferType.HandGrenade,
            label = "Hand Grenades x2",
            detail = "Heavy blast damage.",
            price = gm.GetMerchantPrice(480),
            available = true
        };

        offers[5] = new MerchantOffer
        {
            type = MerchantOfferType.FlashGrenade,
            label = "Flash Grenade",
            detail = "Wide stun to stop a rush.",
            price = gm.GetMerchantPrice(620),
            available = true
        };

        offers[6] = new MerchantOffer
        {
            type = MerchantOfferType.IncendiaryGrenade,
            label = "Incendiary Grenade",
            detail = "Burns enemies over time.",
            price = gm.GetMerchantPrice(720),
            available = true
        };

        offers[7] = new MerchantOffer
        {
            type = MerchantOfferType.PunisherReward,
            label = gm.GetBlueMedallionRewardLabel(),
            detail = gm.GetBlueMedallionRewardDetail(),
            price = 0,
            available = gm.CanClaimPunisherReward(),
            unavailableReason = gm.blueRewardClaimed ? "Reward already claimed" :
                (gm.blueRequestActive ? "Destroy 10 blue medallions" : "Find the blue request note")
        };

        offers[8] = new MerchantOffer
        {
            type = treasureValue > 0 ? MerchantOfferType.SellTreasures : MerchantOfferType.Recovery,
            label = treasureValue > 0 ? "Sell Treasures x" + p.GetTreasureCount() : "Full Recovery",
            detail = treasureValue > 0 ? p.GetTreasureSummary() : "Restores Leon to full life.",
            price = treasureValue > 0 ? treasureValue : gm.GetMerchantPrice(420),
            grantsMoney = treasureValue > 0,
            available = treasureValue > 0 || p.hp < p.maxHp - .01f,
            unavailableReason = treasureValue > 0 ? "" : "Health already full"
        };

        offers[9] = BuildWeaponOffer(gm, p, nextWeapon);
        offers[10] = BuildUpgradeOffer(gm, p, Player.UpgradeCategory.Firepower, MerchantOfferType.UpgradeFirepower, "Firepower Tune-Up");
        offers[11] = BuildUpgradeOffer(gm, p, Player.UpgradeCategory.Reload, MerchantOfferType.UpgradeReload, "Reload Tune-Up");
        offers[12] = BuildUpgradeOffer(gm, p, Player.UpgradeCategory.Capacity, MerchantOfferType.UpgradeCapacity, "Capacity Tune-Up");
        offers[13] = BuildExclusiveOffer(gm, p);
        offers[14] = new MerchantOffer
        {
            type = MerchantOfferType.CaseUpgrade,
            label = "Case Upgrade",
            detail = "More slots and easier packing.",
            price = gm.GetMerchantPrice(caseBasePrice),
            available = AttacheCase.I && !AttacheCase.I.IsMaxSize,
            unavailableReason = "Case already maxed"
        };

        return offers;
    }

    MerchantOffer BuildWeaponOffer(GameManager gm, Player p, int weaponIdx)
    {
        if (weaponIdx < 0)
        {
            return new MerchantOffer
            {
                type = MerchantOfferType.WeaponUnlock,
                label = "New Weapon",
                detail = "All merchant weapons acquired.",
                available = false,
                unavailableReason = "Inventory complete"
            };
        }

        Vector2Int footprint = AttacheCase.GetWeaponFootprint(weaponIdx);
        bool hasSpace = !AttacheCase.I || AttacheCase.I.HasFreeSpace(footprint.x, footprint.y);
        return new MerchantOffer
        {
            type = MerchantOfferType.WeaponUnlock,
            weaponIdx = weaponIdx,
            label = p.weapons[weaponIdx].name,
            detail = "Unlocks a new weapon loadout.",
            price = gm.GetMerchantPrice(GetWeaponBasePrice(weaponIdx)),
            available = hasSpace,
            unavailableReason = hasSpace ? "" : "Need more case space"
        };
    }

    MerchantOffer BuildUpgradeOffer(GameManager gm, Player p, Player.UpgradeCategory category, MerchantOfferType type, string label)
    {
        bool available = p.CanUpgradeWeapon(p.curWeapon, category);
        string reason = available ? "" : "Upgrade maxed";
        if (p.curWeapon == 4) reason = "Rocket cannot be tuned";

        return new MerchantOffer
        {
            type = type,
            weaponIdx = p.curWeapon,
            upgradeCategory = category,
            label = label,
            detail = BuildUpgradeDetailText(p, category),
            price = gm.GetMerchantPrice(p.GetUpgradeCost(p.curWeapon, category)),
            available = available,
            unavailableReason = reason
        };
    }

    MerchantOffer BuildExclusiveOffer(GameManager gm, Player p)
    {
        bool available = p.CanUpgradeExclusive(p.curWeapon);
        string reason = available ? "" :
            p.curWeapon == 4 ? "Rocket cannot be tuned" :
            p.weapons[p.curWeapon].exclusiveUnlocked ? "Exclusive already unlocked" :
            "Max base tune-ups first";

        return new MerchantOffer
        {
            type = MerchantOfferType.UpgradeExclusive,
            weaponIdx = p.curWeapon,
            label = p.GetExclusiveUpgradeName(p.curWeapon),
            detail = p.GetExclusiveUpgradeDetail(p.curWeapon),
            price = gm.GetMerchantPrice(p.GetExclusiveUpgradeCost(p.curWeapon)),
            available = available,
            unavailableReason = reason
        };
    }

    string BuildMerchantOfferLabel(MerchantOffer offer)
    {
        string priceText = offer.available ? (offer.grantsMoney ? "+$" + offer.price : "$" + offer.price) : offer.unavailableReason;
        string detailText = string.IsNullOrEmpty(offer.detail) ? "" : "\n<size=11>" + offer.detail + "  " + priceText + "</size>";
        return offer.label + detailText;
    }

    string BuildMerchantSummary(GameManager gm, Player p)
    {
        string accuracy = gm.lastWaveBonus > 0 ? Mathf.RoundToInt(gm.lastWaveAccuracy * 100f) + "%" : "--";
        string clearState = gm.lastWavePerfect ? "Perfect clear" : (gm.lastWaveBonus > 0 ? "Area cleared" : "Restock phase");
        return "Merchant Ledger\n\n" +
               "Pesetas: " + p.money + "\n" +
               "Herbs: " + p.GetHerbInventorySummary() + "\n" +
               "Grenades: " + p.GetGrenadeInventorySummary() + "\n" +
               "Request: " + gm.GetBlueMedallionStatusText() + "\n" +
               "Treasures: " + p.GetTreasureSummary() + "\n" +
               "Discount: " + gm.merchantDiscountPercent + "%\n" +
               "Wave Bonus: +" + gm.lastWaveBonus + "\n" +
               "Accuracy: " + accuracy + "\n" +
               "Result: " + clearState + "\n" +
               "Case: " + (AttacheCase.I ? AttacheCase.I.gridW + "x" + AttacheCase.I.gridH : "--");
    }

    string BuildMerchantWeaponText(Player p)
    {
        var weapon = p.weapons[p.curWeapon];
        return "Current Weapon\n\n" +
               weapon.name.ToUpper() + "\n" +
               "Damage: " + weapon.dmg + "\n" +
               "Reload: " + weapon.reloadTime.ToString("F2") + "s\n" +
               "Magazine: " + weapon.ammoInMag + "/" + weapon.magSize + "\n" +
               "Reserve: " + weapon.ammoReserve + "\n" +
               "Spread: " + weapon.spread.ToString("F2") + "\n" +
               "Pierce: " + (weapon.pierceCount + 1) + " target(s)\n" +
               "Exclusive: " + (weapon.exclusiveUnlocked ? "Unlocked" : (p.CanUpgradeExclusive(p.curWeapon) ? "Ready" : "Locked")) + "\n" +
               p.GetWeaponLevelSummary(p.curWeapon);
    }

    string BuildMerchantMetaText(GameManager gm, Player p)
    {
        string perfect = gm.lastWavePerfect ? "Perfect bonus secured. Discounts are at their highest." :
            "Push for cleaner waves to increase the merchant discount.";
        return "Notes\n\n" +
               perfect + "\n\n" +
               (p.GetTreasureCount() > 0 ? "Sell valuables before the next wave.\n" : "Break crates and kill elites for valuable treasure drops.\n") +
               "Blue Medallions unlock the Punisher reward at the merchant.\n" +
               "Herb mixes: G heals, G+R full heal, G+Y raises max life, G+R+Y does both.\n" +
               "Cycle grenade types with B before throwing.\n" +
               "Exclusive tune-up unlocks after maxing firepower, reload and capacity.\n" +
               "Capacity upgrades fully refill the magazine.\n" +
               "Open the Attache Case with TAB while the merchant is active.\n" +
               "Auto-sort inside the case with T.";
    }

    string BuildUpgradeDetailText(Player p, Player.UpgradeCategory category)
    {
        var weapon = p.weapons[p.curWeapon];
        switch (category)
        {
            case Player.UpgradeCategory.Firepower:
                return "Current damage " + weapon.dmg + ".";
            case Player.UpgradeCategory.Reload:
                return "Current reload " + weapon.reloadTime.ToString("F2") + "s.";
            case Player.UpgradeCategory.Capacity:
                return "Current magazine " + weapon.magSize + ".";
            default:
                return "";
        }
    }

    int FindNextWeaponUnlock(Player p)
    {
        for (int i = 1; i < Mathf.Min(p.weapons.Length, 5); i++)
            if (!p.weapons[i].owned)
                return i;
        return -1;
    }

    int GetAmmoBundleAmount(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return 24;
            case 1: return 8;
            case 2: return 6;
            case 3: return 60;
            case 4: return 1;
            default: return 0;
        }
    }

    int GetAmmoBundleBasePrice(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return 160;
            case 1: return 300;
            case 2: return 360;
            case 3: return 260;
            case 4: return 2600;
            default: return 200;
        }
    }

    int GetWeaponBasePrice(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 1: return 2200;
            case 2: return 3200;
            case 3: return 1800;
            case 4: return 5500;
            default: return 1000;
        }
    }

    int GetCaseUpgradeBasePrice()
    {
        if (!AttacheCase.I) return 3200;
        return 2400 + (AttacheCase.I.gridW - 10) * 650 + (AttacheCase.I.gridH - 6) * 900;
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
        t.supportRichText = true;
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
    // ── Checkpoint / encounter messages ───────────────────────────────────
    /// <summary>Show a brief "CHECKPOINT" banner on screen.</summary>
    public void ShowCheckpointMessage(int index)
    {
        StartCoroutine(ShowTemporaryMessage($"✓ CHECKPOINT {index + 1}", new Color32(80, 200, 80, 255), 3f));
    }

    /// <summary>Show "AREA CLEARED" banner when an EnemySpawner finishes.</summary>
    public void ShowEncounterClearedMessage()
    {
        StartCoroutine(ShowTemporaryMessage("★ AREA CLEARED", new Color32(220, 200, 60, 255), 3f));
    }

    IEnumerator ShowTemporaryMessage(string msg, Color32 col, float duration)
    {
        // Create a temporary label in the center of the canvas
        if (!canvas) yield break;
        var go   = new GameObject("TempMsg");
        go.transform.SetParent(canvas.transform, false);
        var txt  = go.AddComponent<Text>();
        txt.text      = msg;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = col;
        txt.alignment = TextAnchor.MiddleCenter;
        var rt = txt.rectTransform;
        rt.anchorMin  = new Vector2(0.2f, 0.6f);
        rt.anchorMax  = new Vector2(0.8f, 0.7f);
        rt.offsetMin  = rt.offsetMax = Vector2.zero;

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha  = t < 0.3f ? t / 0.3f : (t > duration - 0.5f ? (duration - t) / 0.5f : 1f);
            txt.color    = new Color32(col.r, col.g, col.b, (byte)(alpha * 255));
            yield return null;
        }
        Destroy(go);
    }

    public void ShowStatusMessage(string msg, Color32 col, float duration = 2f)
    {
        statusText.text = msg;
        statusColor = col;
        statusDuration = Mathf.Max(.1f, duration);
        statusTimer = statusDuration;
    }

    public void ShowPickupMessage(string msg, Color32 col, float duration = 1.8f)
    {
        pickupText.text = msg;
        pickupColor = col;
        pickupDuration = Mathf.Max(.1f, duration);
        pickupTimer = pickupDuration;
    }

    string BuildObjectiveText(GameManager gm)
    {
        switch (gm.State)
        {
            case GameManager.GState.Title:
                return "";
            case GameManager.GState.WaveIntro:
                return $"Incoming wave {gm.wave}. Hold your ground.";
            case GameManager.GState.Merchant:
                return $"Restock and tune up. Discount {gm.merchantDiscountPercent}%  |  Bonus +{gm.lastWaveBonus}.";
            case GameManager.GState.Dead:
                return "You are dead.";
            default:
                if (gm.blueRequestActive && !gm.blueRewardClaimed && gm.blueMedallionsDestroyed < 15)
                    return $"Blue Medallions {gm.blueMedallionsDestroyed}/15. Keep searching the village.";
                if (gm.enemiesAlive <= 0) return "Sweep the area.";
                if (gm.enemiesAlive <= 3) return $"Finish the last {gm.enemiesAlive} ganado(s).";
                return $"Clear the village. {gm.enemiesAlive} hostiles remaining.";
        }
    }

    string BuildHintText(GameManager gm, Player p, Player.WeaponData weapon, float hpPct)
    {
        if (AttacheCase.I && AttacheCase.I.IsOpen)
            return "[T] Auto-sort   [R] Rotate   [1-5] Equip";

        if (p.IsGrabbed)
            return "[F] [E] Break Free";

        if (gm.State == GameManager.GState.Merchant &&
            Vector3.Distance(p.transform.position, gm.merchantPos) < 4f)
            return merchantOpen ? "[ESC] Close Merchant   [TAB] Attache Case" : "[E] Talk to Merchant   [TAB] Attache Case";

        if (p.CanUseHealingShortcut() && (hpPct < .45f || p.yellowHerbs > 0))
            return "[H] " + p.GetHealingShortcutLabel();

        if (weapon.ammoInMag == 0 && weapon.ammoReserve > 0)
            return "[R] Reload";

        if (p.HasAnyGrenades() && gm.enemiesAlive >= 4)
            return "[B] Cycle Grenade   [G] Throw " + p.GetSelectedGrenadeLabel();

        if (gm.State == GameManager.GState.Playing && gm.enemiesAlive > 0)
            return "[TAB] Attache Case   [S]+[Shift] Quick Turn   [F5] Save   [F9] Load";

        return "";
    }

    void UpdateTransientLabel(Text label, ref float timer, float duration, Color baseColor)
    {
        if (!label) return;

        if (timer <= 0f)
        {
            label.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0);
            return;
        }

        timer -= Time.deltaTime;
        float fadeWindow = Mathf.Min(.35f, duration * .35f);
        float alpha = timer < fadeWindow ? timer / fadeWindow : 1f;
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
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
        txt.supportRichText = true;
        var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }
}
