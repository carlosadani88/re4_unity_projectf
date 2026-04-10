using UnityEngine;

public class BlueMedallionNotice : MonoBehaviour
{
    public string message = "Blue request started.";
    bool collected;

    void Awake()
    {
        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = .8f;
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (!player) return;

        collected = true;
        GameManager.I?.ActivateBlueMedallionRequest();
        if (GameUI.I)
            GameUI.I.ShowPickupMessage(message, new Color32(80, 150, 255, 255), 2f);
        gameObject.SetActive(false);
    }
}
