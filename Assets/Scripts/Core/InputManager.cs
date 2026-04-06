// ============================================================================
// VILL4GE — InputManager.cs
// Centralized input abstraction. Wraps Unity's legacy Input system so that
// swapping to the new Input System package requires only one file change.
// Usage: InputManager.I.AimHeld, InputManager.I.FireDown, etc.
// ============================================================================
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager I { get; private set; }

    // ── Axes ───────────────────────────────────────────────────────────────
    public float MoveX        => Input.GetAxisRaw("Horizontal");
    public float MoveZ        => Input.GetAxisRaw("Vertical");
    public float MouseX       => Input.GetAxis("Mouse X");
    public float MouseY       => Input.GetAxis("Mouse Y");
    public float ScrollWheel  => Input.GetAxis("Mouse ScrollWheel");

    // ── Buttons (hold) ────────────────────────────────────────────────────
    public bool SprintHeld  => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    public bool AimHeld     => Input.GetMouseButton(1);
    public bool FireHeld    => Input.GetMouseButton(0);
    public bool CrouchHeld  => Input.GetKey(KeyCode.C);

    // ── Buttons (down) ────────────────────────────────────────────────────
    public bool FireDown        => Input.GetMouseButtonDown(0);
    public bool AimDown         => Input.GetMouseButtonDown(1);
    public bool ReloadDown      => Input.GetKeyDown(KeyCode.R);
    public bool KickDown        => Input.GetKeyDown(KeyCode.F);
    public bool GrenadeDown     => Input.GetKeyDown(KeyCode.G);
    public bool HealDown        => Input.GetKeyDown(KeyCode.H);
    public bool InteractDown    => Input.GetKeyDown(KeyCode.E);
    public bool InventoryToggle => Input.GetKeyDown(KeyCode.Tab);
    public bool PauseToggle     => Input.GetKeyDown(KeyCode.Escape);
    public bool KnifeDown       => Input.GetKeyDown(KeyCode.V);
    public bool MapDown         => Input.GetKeyDown(KeyCode.M);
    public bool QuickSave       => Input.GetKeyDown(KeyCode.F5);
    public bool QuickLoad       => Input.GetKeyDown(KeyCode.F9);

    // ── Weapon selection (1-6) ────────────────────────────────────────────
    public int WeaponSlotDown()
    {
        for (int i = 0; i < 6; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
        return -1;
    }

    // ── Sensitivity (configurable at runtime) ────────────────────────────
    [Range(0.5f, 10f)] public float MouseSensitivity = 3f;
    [Range(0.5f, 10f)] public float ControllerSensitivity = 4f;
    public bool InvertY = false;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Applies sensitivity + invert to a raw mouse Y value.
    /// </summary>
    public float AppliedMouseY => MouseY * (InvertY ? -1f : 1f);
}
