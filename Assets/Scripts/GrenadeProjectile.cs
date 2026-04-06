// ============================================================================
// VILL4GE — GrenadeProjectile.cs  (RE4-style)
// Timer 2s, explosão com dano em área, flash visual, auto-dano.
// ============================================================================
using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    public float damage = 220;
    float timer = 2f;
    const float RADIUS = 6f;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0) return;

        // Dano a inimigos
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < RADIUS) e.TakeDamage(damage * (1 - d / RADIUS), false);
        }
        // Auto-dano
        var p = GameManager.I?.player;
        if (p) { float pd = Vector3.Distance(transform.position, p.transform.position); if (pd < RADIUS) p.TakeDamage(60 * (1 - pd / RADIUS)); }

        // Visual: esfera de fogo + luz temporária
        var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = transform.position;
        fx.transform.localScale = Vector3.one * RADIUS * .5f;
        Destroy(fx.GetComponent<Collider>());
        fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 120, 25, 200), new Color(4, 1.5f, .2f));
        // Luz de explosão
        var lo = new GameObject("ExpLight"); lo.transform.position = transform.position;
        var pl = lo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1, .6f, .15f);
        pl.range = 15; pl.intensity = 4;
        Destroy(lo, .3f);
        Destroy(fx, .3f);
        Destroy(gameObject);
    }
}
