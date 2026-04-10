// ============================================================================
// VILL4GE — SaveSystem.cs
// JSON-based save / load using Application.persistentDataPath.
// Saves: player HP, weapons/ammo, pesetas, wave, kills, position.
//
// Quick save:  F5            Quick load: F9
// Auto-save at checkpoints (see CheckpointSystem.cs).
//
// Save files live in Application.persistentDataPath/saves/
// ============================================================================
using UnityEngine;
using System.IO;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem I { get; private set; }

    const int MAX_SLOTS = 5;
    static string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

    // ── Save data structure ───────────────────────────────────────────────
    [System.Serializable]
    public class SaveData
    {
        // Meta
        public string version  = "1.5";
        public string dateTime;
        public string state;

        // Player
        public float  playerHp;
        public float  playerHpMax;
        public float  px, py, pz;   // position
        public float  pry;           // rotation Y

        // Inventory
        public int    pesetas;
        public int    currentWeapon;
        public int[]  ammo = new int[8];
        public int[]  reserveAmmo = new int[8];
        public bool[] ownedWeapons = new bool[8];
        public bool[] exclusiveWeapons = new bool[8];
        public int[]  weaponFirepowerLevels = new int[8];
        public int[]  weaponReloadLevels = new int[8];
        public int[]  weaponCapacityLevels = new int[8];
        public int    herbs;
        public int    redHerbs;
        public int    yellowHerbs;
        public int    grenades;
        public int    flashGrenades;
        public int    incendiaryGrenades;
        public int    selectedGrenadeType;
        public bool   punisherUnlocked;
        public bool   punisherSpecialUnlocked;
        public int    spinels;
        public int    rubies;
        public int    pendants;
        public float  damageMultiplier;
        public int    caseWidth;
        public int    caseHeight;

        // Progress
        public int wave;
        public int kills;
        public int enemiesRemaining;
        public bool blueNoteCollected;
        public bool blueRequestActive;
        public int blueMedallionsDestroyed;
        public bool blueRewardClaimed;
        public bool[] blueMedallionDestroyedFlags = new bool[15];
        public int merchantDiscountPercent;
        public int lastWaveBonus;
        public float lastWaveAccuracy;
        public bool lastWavePerfect;
        public int checkpointIndex;
    }

    // ── Quick save state ─────────────────────────────────────────────────
    public SaveData LastSave { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        EnsureSaveDir();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) QuickSave();
        if (Input.GetKeyDown(KeyCode.F9)) QuickLoad();
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Save game state to a numbered slot (0 = quick-save).</summary>
    public bool Save(int slot = 0, bool silent = false)
    {
        if (!IsSlotValid(slot)) return false;

        var data = CollectData();
        if (data == null) return false;

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        string path = SlotPath(slot);

        try
        {
            File.WriteAllText(path, json);
            LastSave = data;
            Debug.Log($"[SaveSystem] Saved to slot {slot}: {path}");
            if (!silent)
            {
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.CheckpointSave);
                if (GameUI.I) GameUI.I.ShowStatusMessage("GAME SAVED", new Color32(120, 210, 255, 255), 2f);
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSystem] Save failed: {ex.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Load game state from a numbered slot (0 = quick-save).</summary>
    public bool Load(int slot = 0)
    {
        if (!IsSlotValid(slot)) return false;

        string path = SlotPath(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSystem] No save in slot {slot}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return false;

            ApplyData(data);
            LastSave = data;
            Debug.Log($"[SaveSystem] Loaded slot {slot}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSystem] Load failed: {ex.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    public void QuickSave() => Save(0);
    public void QuickLoad() => Load(0);

    /// <summary>Returns true if the given slot has a save file.</summary>
    public bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    /// <summary>Returns save metadata for the slot, or null.</summary>
    public SaveData PeekSlot(int slot)
    {
        if (!SlotExists(slot)) return null;
        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
        }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────────
    SaveData CollectData()
    {
        var p = GameManager.I?.player;
        if (p == null) return null;

        var data = new SaveData();
        data.dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        data.playerHp    = p.hp;
        data.playerHpMax = p.maxHp;

        var pos = p.transform.position;
        data.px  = pos.x; data.py = pos.y; data.pz = pos.z;
        data.pry = p.transform.eulerAngles.y;

        data.pesetas       = p.money;
        data.currentWeapon = p.curWeapon;
        data.herbs         = p.herbs;
        data.redHerbs      = p.redHerbs;
        data.yellowHerbs   = p.yellowHerbs;
        data.grenades      = p.grenades;
        data.flashGrenades = p.flashGrenades;
        data.incendiaryGrenades = p.incendiaryGrenades;
        data.selectedGrenadeType = (int)p.selectedGrenade;
        data.punisherUnlocked = p.punisherUnlocked;
        data.punisherSpecialUnlocked = p.punisherSpecialUnlocked;
        data.spinels       = p.spinels;
        data.rubies        = p.rubies;
        data.pendants      = p.pendants;
        data.damageMultiplier = p.dmgMultiplier;
        data.state = GameManager.I.State.ToString();

        if (p.weapons != null)
            for (int i = 0; i < Mathf.Min(p.weapons.Length, 8); i++)
            {
                data.ammo[i] = p.weapons[i].ammoInMag;
                data.reserveAmmo[i] = p.weapons[i].ammoReserve;
                data.ownedWeapons[i] = p.weapons[i].owned;
                data.exclusiveWeapons[i] = p.weapons[i].exclusiveUnlocked;
                data.weaponFirepowerLevels[i] = p.weapons[i].firepowerLevel;
                data.weaponReloadLevels[i] = p.weapons[i].reloadLevel;
                data.weaponCapacityLevels[i] = p.weapons[i].capacityLevel;
            }

        if (AttacheCase.I)
        {
            data.caseWidth = AttacheCase.I.gridW;
            data.caseHeight = AttacheCase.I.gridH;
        }

        data.wave  = GameManager.I.wave;
        data.kills = GameManager.I.kills;
        data.enemiesRemaining = GameManager.I.enemiesAlive;
        data.blueNoteCollected = GameManager.I.blueNoticeCollected;
        data.blueRequestActive = GameManager.I.blueRequestActive;
        data.blueMedallionsDestroyed = GameManager.I.blueMedallionsDestroyed;
        data.blueRewardClaimed = GameManager.I.blueRewardClaimed;
        data.blueMedallionDestroyedFlags = GameManager.I.GetBlueMedallionFlagsCopy();
        data.merchantDiscountPercent = GameManager.I.merchantDiscountPercent;
        data.lastWaveBonus = GameManager.I.lastWaveBonus;
        data.lastWaveAccuracy = GameManager.I.lastWaveAccuracy;
        data.lastWavePerfect = GameManager.I.lastWavePerfect;

        return data;
    }

    void ApplyData(SaveData data)
    {
        var p = GameManager.I?.player;
        if (p == null) return;
        bool hasExtendedInventory = !string.IsNullOrEmpty(data.version) && data.version != "1.0";
        bool hasMerchantMeta = hasExtendedInventory;

        p.hp      = data.playerHp;
        p.maxHp   = data.playerHpMax;
        p.money   = data.pesetas;
        p.herbs   = data.herbs;
        p.redHerbs = hasExtendedInventory ? data.redHerbs : 0;
        p.yellowHerbs = hasExtendedInventory ? data.yellowHerbs : 0;
        p.grenades = hasExtendedInventory ? data.grenades : p.grenades;
        p.flashGrenades = hasExtendedInventory ? data.flashGrenades : 0;
        p.incendiaryGrenades = hasExtendedInventory ? data.incendiaryGrenades : 0;
        p.selectedGrenade = (Player.GrenadeType)Mathf.Clamp(data.selectedGrenadeType, 0, 2);
        p.punisherUnlocked = hasExtendedInventory && data.punisherUnlocked;
        p.punisherSpecialUnlocked = hasExtendedInventory && data.punisherSpecialUnlocked;
        p.spinels = hasExtendedInventory ? data.spinels : 0;
        p.rubies = hasExtendedInventory ? data.rubies : 0;
        p.pendants = hasExtendedInventory ? data.pendants : 0;
        p.dmgMultiplier = hasExtendedInventory && data.damageMultiplier > 0 ? data.damageMultiplier : 1f;

        p.transform.position = new Vector3(data.px, data.py, data.pz);
        p.transform.eulerAngles = new Vector3(0, data.pry, 0);

        if (p.weapons != null)
            for (int i = 0; i < Mathf.Min(p.weapons.Length, 8); i++)
            {
                if (data.ammo != null && i < data.ammo.Length)
                    p.weapons[i].ammoInMag = data.ammo[i];
                if (hasExtendedInventory && data.reserveAmmo != null && i < data.reserveAmmo.Length)
                    p.weapons[i].ammoReserve = data.reserveAmmo[i];
                if (hasExtendedInventory && data.ownedWeapons != null && i < data.ownedWeapons.Length)
                    p.weapons[i].owned = data.ownedWeapons[i];
                if (hasExtendedInventory && data.exclusiveWeapons != null && i < data.exclusiveWeapons.Length)
                    p.weapons[i].exclusiveUnlocked = data.exclusiveWeapons[i];
                if (hasExtendedInventory && data.weaponFirepowerLevels != null && i < data.weaponFirepowerLevels.Length)
                    p.weapons[i].firepowerLevel = data.weaponFirepowerLevels[i];
                if (hasExtendedInventory && data.weaponReloadLevels != null && i < data.weaponReloadLevels.Length)
                    p.weapons[i].reloadLevel = data.weaponReloadLevels[i];
                if (hasExtendedInventory && data.weaponCapacityLevels != null && i < data.weaponCapacityLevels.Length)
                    p.weapons[i].capacityLevel = data.weaponCapacityLevels[i];
            }

        p.RefreshAllWeaponStats();

        if (AttacheCase.I)
        {
            AttacheCase.I.SetSize(
                hasExtendedInventory && data.caseWidth > 0 ? data.caseWidth : 10,
                hasExtendedInventory && data.caseHeight > 0 ? data.caseHeight : 6);
            AttacheCase.I.RebuildWeaponsFromPlayer(p);
        }

        p.SwitchToSlot(data.currentWeapon);
        p.ValidateGrenadeSelection();
        if (GameManager.I)
        {
            GameManager.I.ApplyBlueMedallionState(
                hasMerchantMeta && data.blueNoteCollected,
                hasMerchantMeta && data.blueRequestActive,
                hasMerchantMeta ? data.blueMedallionsDestroyed : 0,
                hasMerchantMeta && data.blueRewardClaimed,
                hasMerchantMeta ? data.blueMedallionDestroyedFlags : null);

            GameManager.GState savedState = GameManager.GState.Playing;
            if (!string.IsNullOrEmpty(data.state))
                System.Enum.TryParse(data.state, out savedState);

            GameManager.I.RestoreProgress(
                data.wave,
                data.kills,
                data.enemiesRemaining,
                savedState,
                hasMerchantMeta ? data.merchantDiscountPercent : 0,
                hasMerchantMeta ? data.lastWaveBonus : 0,
                hasMerchantMeta ? data.lastWaveAccuracy : 0f,
                hasMerchantMeta && data.lastWavePerfect);
        }

        if (GameUI.I)
            GameUI.I.ShowStatusMessage("GAME LOADED", new Color32(120, 200, 255, 255), 2f);
    }

    // ─────────────────────────────────────────────────────────────────────
    static string SlotPath(int slot) =>
        Path.Combine(SaveDir, $"save_slot_{slot:D2}.json");

    static bool IsSlotValid(int slot) => slot >= 0 && slot < MAX_SLOTS;

    static void EnsureSaveDir()
    {
        if (!Directory.Exists(SaveDir))
            Directory.CreateDirectory(SaveDir);
    }
}
