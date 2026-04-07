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
        public string version  = "1.0";
        public string dateTime;

        // Player
        public float  playerHp;
        public float  playerHpMax;
        public float  px, py, pz;   // position
        public float  pry;           // rotation Y

        // Inventory
        public int    pesetas;
        public int    currentWeapon;
        public int[]  ammo = new int[8];
        public int    herbs;

        // Progress
        public int wave;
        public int kills;
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
    public bool Save(int slot = 0)
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
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.CheckpointSave);
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

        if (p.weapons != null)
            for (int i = 0; i < Mathf.Min(p.weapons.Length, 8); i++)
                data.ammo[i] = p.weapons[i].ammoInMag;

        data.wave  = GameManager.I.wave;
        data.kills = GameManager.I.kills;

        return data;
    }

    void ApplyData(SaveData data)
    {
        var p = GameManager.I?.player;
        if (p == null) return;

        p.hp      = data.playerHp;
        p.maxHp   = data.playerHpMax;
        p.money   = data.pesetas;
        p.herbs   = data.herbs;

        p.transform.position = new Vector3(data.px, data.py, data.pz);
        p.transform.eulerAngles = new Vector3(0, data.pry, 0);

        if (p.weapons != null)
            for (int i = 0; i < Mathf.Min(p.weapons.Length, 8); i++)
                p.weapons[i].ammoInMag = data.ammo[i];

        p.SwitchToSlot(data.currentWeapon);

        GameManager.I.wave  = data.wave;
        GameManager.I.kills = data.kills;
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
