// ============================================================================
// VILL4GE - Pickup.cs  (RE4-style)
// Rotating drops with emissive visuals. Herbs, typed ammo, pesetas, grenades
// and treasures tuned to the player's current state.
// ============================================================================
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum PType { Herb, Ammo, Money, Grenade, Treasure }
    public enum TreasureType { Spinel, Ruby, Pendant }
    public enum HerbType { Green, Red, Yellow }

    public PType type;
    public TreasureType treasureType;
    public HerbType herbType = HerbType.Green;
    public Player.GrenadeType grenadeType = Player.GrenadeType.Hand;
    public int ammoWeaponIdx = -1;
    int value;
    float baseY;

    public void Init(bool richDrop = false)
    {
        Player player = GameManager.I ? GameManager.I.player : null;
        if (player)
            RollAdaptiveDrop(player, richDrop);
        else
            RollFallbackDrop(richDrop);

        BuildVisual();
    }

    public void InitTreasure(TreasureType treasure, int amount = 1)
    {
        type = PType.Treasure;
        treasureType = treasure;
        value = Mathf.Max(1, amount);
        ammoWeaponIdx = -1;
        BuildVisual();
    }

    public void InitMoney(int amount)
    {
        type = PType.Money;
        value = Mathf.Max(1, amount);
        ammoWeaponIdx = -1;
        BuildVisual();
    }

    void RollAdaptiveDrop(Player player, bool richDrop)
    {
        int herbWeight = HerbWeight(player, richDrop);
        int moneyWeight = richDrop ? 25 : 20;
        int grenadeWeight = GrenadeWeight(player, richDrop);
        int treasureWeight = richDrop ? 11 : 2;
        int[] ammoWeights = new int[Mathf.Min(player.weapons.Length, 4)];
        int ammoWeightTotal = 0;

        for (int i = 0; i < ammoWeights.Length; i++)
        {
            ammoWeights[i] = AmmoWeight(player, i, richDrop);
            ammoWeightTotal += ammoWeights[i];
        }

        int totalWeight = herbWeight + moneyWeight + grenadeWeight + treasureWeight + ammoWeightTotal;
        if (totalWeight <= 0)
        {
            RollFallbackDrop(richDrop);
            return;
        }

        int roll = Random.Range(0, totalWeight);
        if (roll < herbWeight)
        {
            type = PType.Herb;
            herbType = ChooseHerbDrop(player, richDrop);
            value = 1;
            ammoWeaponIdx = -1;
            return;
        }
        roll -= herbWeight;

        if (roll < ammoWeightTotal)
        {
            type = PType.Ammo;
            ammoWeaponIdx = PickAmmoWeapon(ammoWeights);
            value = AmmoDropAmount(ammoWeaponIdx, richDrop);
            return;
        }
        roll -= ammoWeightTotal;

        if (roll < moneyWeight)
        {
            type = PType.Money;
            value = MoneyDropAmount(richDrop);
            ammoWeaponIdx = -1;
            return;
        }
        roll -= moneyWeight;

        if (roll < grenadeWeight)
        {
            type = PType.Grenade;
            grenadeType = ChooseGrenadeDrop(player, richDrop);
            value = 1;
            ammoWeaponIdx = -1;
            return;
        }

        float treasureRoll = Random.value;
        if (treasureRoll < .55f) treasureType = TreasureType.Spinel;
        else if (treasureRoll < .87f) treasureType = TreasureType.Ruby;
        else treasureType = TreasureType.Pendant;

        type = PType.Treasure;
        value = 1;
        ammoWeaponIdx = -1;
    }

    void RollFallbackDrop(bool richDrop)
    {
        float r = Random.value;
        herbType = HerbType.Green;
        grenadeType = Player.GrenadeType.Hand;

        if (richDrop && r < .2f)
        {
            float treasureRoll = Random.value;
            if (treasureRoll < .55f) treasureType = TreasureType.Spinel;
            else if (treasureRoll < .87f) treasureType = TreasureType.Ruby;
            else treasureType = TreasureType.Pendant;

            type = PType.Treasure;
            value = 1;
            ammoWeaponIdx = -1;
            return;
        }

        if (richDrop && r < .34f)
        {
            type = PType.Grenade;
            grenadeType = Random.value < .2f ? Player.GrenadeType.Flash :
                (Random.value < .42f ? Player.GrenadeType.Incendiary : Player.GrenadeType.Hand);
            value = 1;
            ammoWeaponIdx = -1;
        }
        else if (r < .28f)
        {
            type = PType.Herb;
            value = 1;
            ammoWeaponIdx = -1;
        }
        else if (r < .62f)
        {
            type = PType.Ammo;
            ammoWeaponIdx = 0;
            value = AmmoDropAmount(ammoWeaponIdx, richDrop);
        }
        else
        {
            type = PType.Money;
            value = MoneyDropAmount(richDrop);
            ammoWeaponIdx = -1;
        }
    }

    HerbType ChooseHerbDrop(Player player, bool richDrop)
    {
        float roll = Random.value;
        if ((richDrop || player.maxHp < 120f) && player.yellowHerbs <= 0 && player.maxHp < 160f && roll < .18f)
            return HerbType.Yellow;
        if ((richDrop || player.herbs > 0) && player.redHerbs <= 1 && roll < .46f)
            return HerbType.Red;
        return HerbType.Green;
    }

    Player.GrenadeType ChooseGrenadeDrop(Player player, bool richDrop)
    {
        float roll = Random.value;
        if ((richDrop || player.flashGrenades <= 0) && player.flashGrenades <= 1 && roll < .24f)
            return Player.GrenadeType.Flash;
        if ((richDrop || player.incendiaryGrenades <= 0) && player.incendiaryGrenades <= 1 && roll < .46f)
            return Player.GrenadeType.Incendiary;
        return Player.GrenadeType.Hand;
    }

    void BuildVisual()
    {
        Color32 col;
        PrimitiveType shape;
        Vector3 scale;
        ResolveVisual(out col, out shape, out scale);

        var prim = GameObject.CreatePrimitive(shape);
        prim.transform.SetParent(transform);
        prim.transform.localPosition = Vector3.zero;
        prim.transform.localScale = scale;
        Destroy(prim.GetComponent<Collider>());
        prim.GetComponent<Renderer>().material = GameManager.MatEmissive(col, (Color)col * .4f);

        baseY = transform.position.y;
        var sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = 1.2f;
        sc.isTrigger = true;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        Destroy(gameObject, 25f);
    }

    void ResolveVisual(out Color32 col, out PrimitiveType shape, out Vector3 scale)
    {
        col = new Color32(210, 190, 50, 255);
        shape = PrimitiveType.Cylinder;
        scale = Vector3.one * .2f;

        switch (type)
        {
            case PType.Herb:
                ResolveHerbVisual(out col, out shape, out scale);
                return;
            case PType.Ammo:
                ResolveAmmoVisual(out col, out shape, out scale);
                return;
            case PType.Grenade:
                ResolveGrenadeVisual(out col, out shape, out scale);
                return;
            case PType.Treasure:
                ResolveTreasureVisual(out col, out shape, out scale);
                return;
            case PType.Money:
                col = new Color32(210, 190, 50, 255);
                shape = PrimitiveType.Cylinder;
                scale = new Vector3(.17f, .1f, .17f);
                return;
        }
    }

    void ResolveHerbVisual(out Color32 col, out PrimitiveType shape, out Vector3 scale)
    {
        shape = PrimitiveType.Sphere;
        scale = new Vector3(.18f, .22f, .18f);

        switch (herbType)
        {
            case HerbType.Red:
                col = new Color32(180, 50, 55, 255);
                break;
            case HerbType.Yellow:
                col = new Color32(215, 190, 70, 255);
                break;
            default:
                col = new Color32(25, 160, 35, 255);
                break;
        }
    }

    void ResolveAmmoVisual(out Color32 col, out PrimitiveType shape, out Vector3 scale)
    {
        switch (ammoWeaponIdx)
        {
            case 1:
                col = new Color32(190, 60, 45, 255);
                shape = PrimitiveType.Cube;
                scale = new Vector3(.24f, .12f, .18f);
                break;
            case 2:
                col = new Color32(110, 170, 95, 255);
                shape = PrimitiveType.Capsule;
                scale = new Vector3(.12f, .24f, .12f);
                break;
            case 3:
                col = new Color32(95, 145, 210, 255);
                shape = PrimitiveType.Cube;
                scale = new Vector3(.22f, .16f, .22f);
                break;
            default:
                col = new Color32(180, 160, 45, 255);
                shape = PrimitiveType.Cube;
                scale = new Vector3(.18f, .18f, .12f);
                break;
        }
    }

    void ResolveGrenadeVisual(out Color32 col, out PrimitiveType shape, out Vector3 scale)
    {
        shape = PrimitiveType.Capsule;
        scale = Vector3.one * .2f;

        switch (grenadeType)
        {
            case Player.GrenadeType.Flash:
                col = new Color32(180, 190, 215, 255);
                break;
            case Player.GrenadeType.Incendiary:
                col = new Color32(185, 80, 40, 255);
                break;
            default:
                col = new Color32(110, 150, 80, 255);
                break;
        }
    }

    void ResolveTreasureVisual(out Color32 col, out PrimitiveType shape, out Vector3 scale)
    {
        switch (treasureType)
        {
            case TreasureType.Spinel:
                col = new Color32(105, 210, 175, 255);
                shape = PrimitiveType.Sphere;
                scale = Vector3.one * .17f;
                break;
            case TreasureType.Ruby:
                col = new Color32(220, 70, 70, 255);
                shape = PrimitiveType.Sphere;
                scale = Vector3.one * .2f;
                break;
            default:
                col = new Color32(210, 190, 120, 255);
                shape = PrimitiveType.Cylinder;
                scale = new Vector3(.16f, .08f, .16f);
                break;
        }
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3) * .12f;
        transform.position = p;
        transform.Rotate(0, 100 * Time.deltaTime, 0);
    }

    void OnTriggerEnter(Collider other)
    {
        var pl = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (!pl) return;

        string message = "";
        Color32 messageColor = new Color32(220, 200, 120, 255);
        switch (type)
        {
            case PType.Herb:
                switch (herbType)
                {
                    case HerbType.Red:
                        pl.redHerbs += value;
                        message = $"+{value} Red Herb";
                        messageColor = new Color32(220, 100, 95, 255);
                        break;
                    case HerbType.Yellow:
                        pl.yellowHerbs += value;
                        message = $"+{value} Yellow Herb";
                        messageColor = new Color32(225, 205, 105, 255);
                        break;
                    default:
                        pl.herbs += value;
                        message = $"+{value} Green Herb";
                        messageColor = new Color32(90, 210, 105, 255);
                        break;
                }
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.ItemPickup);
                break;
            case PType.Ammo:
                int ammoSlot = ResolveAmmoReceiver(pl);
                pl.weapons[ammoSlot].ammoReserve += value;
                message = $"{AmmoLabel(ammoSlot)} +{value}";
                messageColor = AmmoColor(ammoSlot);
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.AmmoPickup);
                break;
            case PType.Grenade:
                switch (grenadeType)
                {
                    case Player.GrenadeType.Flash:
                        pl.flashGrenades += value;
                        message = $"+{value} Flash Grenade";
                        messageColor = new Color32(210, 220, 255, 255);
                        break;
                    case Player.GrenadeType.Incendiary:
                        pl.incendiaryGrenades += value;
                        message = $"+{value} Incendiary Grenade";
                        messageColor = new Color32(240, 135, 70, 255);
                        break;
                    default:
                        pl.grenades += value;
                        message = $"+{value} Hand Grenade";
                        messageColor = new Color32(150, 210, 120, 255);
                        break;
                }
                pl.ValidateGrenadeSelection();
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.ItemPickup);
                break;
            case PType.Money:
                pl.money += value;
                message = $"PTAS. +{value}";
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.PesetasPickup);
                break;
            case PType.Treasure:
                pl.AddTreasure(treasureType, value);
                message = TreasureLabel(treasureType) + " acquired";
                messageColor = TreasureColor(treasureType);
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.ItemPickup);
                break;
        }

        if (GameUI.I) GameUI.I.ShowPickupMessage(message, messageColor);
        Destroy(gameObject);
    }

    int ResolveAmmoReceiver(Player pl)
    {
        if (ammoWeaponIdx >= 0 &&
            ammoWeaponIdx < pl.weapons.Length &&
            pl.weapons[ammoWeaponIdx].owned &&
            ammoWeaponIdx != 4)
            return ammoWeaponIdx;

        return BestAmmoWeapon(pl);
    }

    int BestAmmoWeapon(Player pl)
    {
        int bestIdx = Mathf.Clamp(pl.curWeapon, 0, Mathf.Min(pl.weapons.Length, 4) - 1);
        float bestNeed = float.MinValue;

        for (int i = 0; i < Mathf.Min(pl.weapons.Length, 4); i++)
        {
            var weapon = pl.weapons[i];
            if (!weapon.owned) continue;

            float need = DesiredAmmoStock(weapon, i) - (weapon.ammoInMag + weapon.ammoReserve);
            if (i == pl.curWeapon) need += AmmoDropAmount(i, false) * .6f;

            if (need > bestNeed)
            {
                bestNeed = need;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    int PickAmmoWeapon(int[] ammoWeights)
    {
        int total = 0;
        for (int i = 0; i < ammoWeights.Length; i++)
            total += ammoWeights[i];

        if (total <= 0) return 0;

        int roll = Random.Range(0, total);
        for (int i = 0; i < ammoWeights.Length; i++)
        {
            if (roll < ammoWeights[i]) return i;
            roll -= ammoWeights[i];
        }

        return 0;
    }

    int HerbWeight(Player player, bool richDrop)
    {
        float hpPct = player.maxHp > .01f ? player.hp / player.maxHp : 1f;
        if (hpPct < .3f) return richDrop ? 24 : 16;
        if (hpPct < .5f) return richDrop ? 16 : 10;
        if ((player.herbs <= 0 || player.redHerbs <= 0) && hpPct < .75f) return 8;
        if (player.maxHp < 140f && player.yellowHerbs <= 0) return 5;
        return richDrop ? 4 : 2;
    }

    int GrenadeWeight(Player player, bool richDrop)
    {
        int totalGrenades = player.grenades + player.flashGrenades + player.incendiaryGrenades;
        int weight = totalGrenades <= 0 ? 7 : 2;
        if (player.flashGrenades <= 0) weight += 1;
        if (player.incendiaryGrenades <= 0) weight += 1;
        if (GameManager.I && GameManager.I.enemiesAlive >= 6) weight += 2;
        if (richDrop) weight += 2;
        return weight;
    }

    int AmmoWeight(Player player, int weaponIdx, bool richDrop)
    {
        if (weaponIdx < 0 || weaponIdx >= Mathf.Min(player.weapons.Length, 4))
            return 0;

        var weapon = player.weapons[weaponIdx];
        if (!weapon.owned) return 0;

        int desired = DesiredAmmoStock(weapon, weaponIdx);
        int current = weapon.ammoInMag + weapon.ammoReserve;
        int deficit = Mathf.Max(0, desired - current);
        int dropAmount = Mathf.Max(1, AmmoDropAmount(weaponIdx, false));
        int weight = Mathf.Clamp(Mathf.CeilToInt((float)deficit / dropAmount), 0, 14);

        if (weaponIdx == player.curWeapon) weight += richDrop ? 5 : 3;
        if (current <= weapon.magSize) weight += 3;

        return weight;
    }

    int DesiredAmmoStock(Player.WeaponData weapon, int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return Mathf.Max(45, weapon.magSize * 5);
            case 1: return Mathf.Max(12, weapon.magSize * 3);
            case 2: return Mathf.Max(9, weapon.magSize * 2);
            case 3: return Mathf.Max(90, weapon.magSize * 3);
            default: return weapon.magSize * 2;
        }
    }

    int AmmoDropAmount(int weaponIdx, bool richDrop)
    {
        switch (weaponIdx)
        {
            case 0: return richDrop ? 15 : 10;
            case 1: return richDrop ? 6 : 4;
            case 2: return richDrop ? 4 : 3;
            case 3: return richDrop ? 50 : 30;
            default: return richDrop ? 12 : 8;
        }
    }

    int MoneyDropAmount(bool richDrop)
    {
        return Random.Range(richDrop ? 450 : 180, richDrop ? 1100 : 520);
    }

    string AmmoLabel(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return "Handgun Ammo";
            case 1: return "Shotgun Shells";
            case 2: return "Rifle Ammo";
            case 3: return "TMP Ammo";
            default: return "Ammo";
        }
    }

    Color32 AmmoColor(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 1: return new Color32(215, 90, 70, 255);
            case 2: return new Color32(120, 190, 105, 255);
            case 3: return new Color32(105, 165, 220, 255);
            default: return new Color32(220, 200, 120, 255);
        }
    }

    string TreasureLabel(TreasureType treasure)
    {
        switch (treasure)
        {
            case TreasureType.Spinel: return "Spinel";
            case TreasureType.Ruby: return "Ruby";
            default: return "Old Pendant";
        }
    }

    Color32 TreasureColor(TreasureType treasure)
    {
        switch (treasure)
        {
            case TreasureType.Spinel: return new Color32(95, 220, 180, 255);
            case TreasureType.Ruby: return new Color32(230, 90, 90, 255);
            default: return new Color32(220, 200, 120, 255);
        }
    }
}
