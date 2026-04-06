// ============================================================================
// VILL4GE — CheckpointSystem.cs
// Place this script on a trigger collider. When the player walks through,
// the game is auto-saved and a "CHECKPOINT" message is shown.
//
// Setup:
//   1. Create an empty GameObject with a BoxCollider (IsTrigger = true).
//   2. Attach this script.
//   3. Set checkpointIndex (unique per checkpoint).
//   4. Optionally assign a spawnPoint Transform (where player respawns).
// ============================================================================
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointSystem : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [Tooltip("Unique index for this checkpoint (used in save data).")]
    public int checkpointIndex = 0;

    [Tooltip("Respawn point for this checkpoint. If null, uses this object's position.")]
    public Transform spawnPoint;

    [Tooltip("Save slot to use (0 = quick-save slot by default).")]
    [Range(0, 4)] public int saveSlot = 0;

    [Tooltip("Show HUD message on activation.")]
    public bool showMessage = true;

    // ── Visual dimensions ────────────────────────────────────────────────
    const float VISUAL_WIDTH   = 2f;
    const float VISUAL_HEIGHT  = 3f;
    const float VISUAL_DEPTH   = 0.1f;

    bool _activated = false;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        // Make sure the collider is a trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // Register spawn point with SaveSystem if needed
        if (!spawnPoint) spawnPoint = transform;

        // Visual indicator (editor only) — a floating semi-transparent cube
        BuildVisual();
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_activated) return;
        if (!other.CompareTag("Player") && !other.GetComponent<Player>()) return;

        _activated = true;
        Activate();
    }

    // ─────────────────────────────────────────────────────────────────────
    void Activate()
    {
        Debug.Log($"[Checkpoint {checkpointIndex}] Activated.");

        // Save the game
        if (SaveSystem.I != null)
            SaveSystem.I.Save(saveSlot);

        // Show HUD message
        if (showMessage && GameUI.I != null)
            GameUI.I.ShowCheckpointMessage(checkpointIndex);

        // Play SFX
        if (AudioManager.I != null)
            AudioManager.I.PlaySFX(AudioManager.SFX.CheckpointSave);

        // Change visual to "activated" color
        SetVisualColor(new Color(0.2f, 0.8f, 0.2f, 0.4f));
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Reset checkpoint (call when restarting wave/area).</summary>
    public void Reset()
    {
        _activated = false;
        SetVisualColor(new Color(0.2f, 0.6f, 1f, 0.3f));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Visual
    GameObject _visual;

    void BuildVisual()
    {
        _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _visual.name = "CheckpointVisual";
        _visual.transform.SetParent(transform);
        _visual.transform.localPosition = Vector3.zero;
        _visual.transform.localScale    = new Vector3(VISUAL_WIDTH, VISUAL_HEIGHT, VISUAL_DEPTH);

        Destroy(_visual.GetComponent<Collider>());

        SetVisualColor(new Color(0.2f, 0.6f, 1f, 0.3f));
    }

    void SetVisualColor(Color c)
    {
        if (!_visual) return;
        var rend = _visual.GetComponent<Renderer>();
        if (!rend) return;

        var mat = GameManager.I
            ? GameManager.Mat(new Color32(
                (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255))
            : new Material(Shader.Find("Standard"));

        mat.color = c;
        // Enable transparency
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        rend.material = mat;
    }
}
