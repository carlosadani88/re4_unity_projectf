// ============================================================================
// VILL4GE — GameManager.cs  (RE4-faithful)
// Vila procedural no estilo real de RE4: igreja com torre/cruz, cemitério
// com lápides, fogueira central, casas com telhado, caminhos de terra,
// cercas de madeira, carroças, feno, tochas com flicker, poços.
// ============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public enum GState { Title, Playing, Merchant, Dead, WaveIntro }
    [HideInInspector] public GState State = GState.Title;
    [HideInInspector] public int wave, kills, enemiesAlive;
    float waveIntroTimer;
    bool waveRewardGranted;
    [HideInInspector] public int merchantDiscountPercent, lastWaveBonus;
    [HideInInspector] public float lastWaveAccuracy;
    [HideInInspector] public bool lastWavePerfect;
    [HideInInspector] public bool blueNoticeCollected, blueRequestActive, blueRewardClaimed;
    [HideInInspector] public int blueMedallionsDestroyed;
    const int BLUE_MEDALLION_TOTAL = 15;
    bool[] blueMedallionFlags = new bool[BLUE_MEDALLION_TOTAL];

    [HideInInspector] public Player player;
    [HideInInspector] public GameUI ui;
    Transform enemyParent;
    Transform medallionParent;
    GameObject merchantObj;
    GameObject blueNoticeObj;
    GState lastMusicState = (GState)(-1);

    public const int MW = 70, MH = 70;
    int[,] map = new int[MW, MH];
    public List<Vector3> spawnPts = new List<Vector3>();
    public Vector3 merchantPos, playerStart;

    // ── Materiais ──────────────────────────────────────────────────────────
    [HideInInspector] public Material matWallExt, matWallInt, matGround, matPath;
    [HideInInspector] public Material matWood, matWoodDark, matTrunk, matStone, matStoneDark;
    [HideInInspector] public Material matSkin, matRoof, matMetal, matFence;
    static Shader _sh;

    public static Material Mat(Color32 c)
    {
        if (!_sh) _sh = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(_sh);
        m.color = c; m.SetFloat("_Glossiness", 0.12f); m.SetFloat("_Metallic", 0f);
        return m;
    }
    public static Material MatEmissive(Color32 c, Color em)
    {
        var m = Mat(c); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", em); return m;
    }

    // ====================================================================
    void Awake()
    {
        if (I) { Destroy(gameObject); return; }
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadows = ShadowQuality.All;
        InitMats();
        GenerateVillage();
        BuildWorld();
        BuildScenery();
        BuildBonfire();
        BuildChurch();
        BuildMerchant();
        BuildBlueMedallionRequest();
        SpawnEnvironmentOverrides();
        SetupLighting();
        SpawnPlayer();
        enemyParent = new GameObject("Enemies").transform;
        ui = new GameObject("UI").AddComponent<GameUI>();
        // Attaché Case (maleta RE4)
        var caseObj = new GameObject("AttacheCase");
        caseObj.AddComponent<AttacheCase>();
        // Core services (lazy-instantiated if not present in scene)
        if (!FindObjectOfType<AudioManager>())
            new GameObject("AudioManager").AddComponent<AudioManager>();
        if (!FindObjectOfType<SaveSystem>())
            new GameObject("SaveSystem").AddComponent<SaveSystem>();
        if (!FindObjectOfType<InputManager>())
            new GameObject("InputManager").AddComponent<InputManager>();
    }

    void InitMats()
    {
        matWallExt  = Mat(new Color32(115, 95, 70, 255));
        matWallInt  = Mat(new Color32(135, 120, 90, 255));
        matGround   = Mat(new Color32(52, 42, 30, 255));
        matPath     = Mat(new Color32(88, 72, 52, 255));
        matWood     = Mat(new Color32(95, 68, 38, 255));
        matWoodDark = Mat(new Color32(50, 34, 18, 255));
        matTrunk    = Mat(new Color32(45, 30, 15, 255));
        matStone    = Mat(new Color32(78, 72, 65, 255));
        matStoneDark= Mat(new Color32(42, 38, 34, 255));
        matSkin     = Mat(new Color32(168, 138, 108, 255));
        matRoof     = Mat(new Color32(80, 48, 28, 255));
        matMetal    = Mat(new Color32(58, 58, 62, 255));
        matFence    = Mat(new Color32(72, 52, 32, 255));
    }

    // ====================================================================
    // GERAÇÃO DA VILA
    // ====================================================================
    void GenerateVillage()
    {
        for (int x = 0; x < MW; x++)
            for (int z = 0; z < MH; z++)
                if (x <= 1 || x >= MW - 2 || z <= 1 || z >= MH - 2) map[x, z] = 1;

        House(8, 8, 16, 14, 12, 8);   House(8, 8, 16, 14, 12, 14);
        House(6, 40, 18, 50, 12, 40);  House(6, 40, 18, 50, 12, 50);
        WallH(10, 14, 45); Door(12, 45);
        House(38, 22, 48, 30, 43, 22); House(38, 22, 48, 30, 43, 30);
        House(50, 45, 62, 58, 56, 45); Door(56, 58);
        House(30, 6, 40, 12, 35, 6);   House(30, 6, 40, 12, 35, 12);
        House(22, 28, 32, 36, 27, 28); House(22, 28, 32, 36, 27, 36);
        WallH(18, 28, 18); Door(23, 18);
        WallH(34, 48, 18); Door(41, 18);
        WallV(48, 12, 20); Door(48, 16);
        WallH(16, 22, 22); WallH(16, 22, 24); Door(19, 22);
        WallV(20, 36, 42); Door(20, 39);

        // Casa extra grande (taverna)
        House(42, 55, 56, 65, 49, 55); House(42, 55, 56, 65, 49, 65);
        WallV(49, 55, 65); Door(49, 60);

        playerStart = new Vector3(12, 0, 11);
        merchantPos = new Vector3(25, 0, 32);

        for (int x = 4; x < MW - 4; x += 3)
            for (int z = 4; z < MH - 4; z += 3)
                if (map[x, z] == 0) spawnPts.Add(new Vector3(x + .5f, 0, z + .5f));
    }
    void House(int x1, int z1, int x2, int z2, int dx, int dz) { Rect(x1, z1, x2, z2); Door(dx, dz); }
    void Rect(int x1, int z1, int x2, int z2)
    { for (int i = x1; i <= x2; i++) { map[i, z1] = 1; map[i, z2] = 1; } for (int i = z1; i <= z2; i++) { map[x1, i] = 1; map[x2, i] = 1; } }
    void Door(int x, int z) { if (x >= 0 && x < MW && z >= 0 && z < MH) map[x, z] = 0; }
    void WallH(int x1, int x2, int z) { for (int x = x1; x <= x2 && x < MW; x++) map[x, z] = 1; }
    void WallV(int x, int z1, int z2) { for (int z = z1; z <= z2 && z < MH; z++) map[x, z] = 1; }

    // ====================================================================
    // MUNDO
    // ====================================================================
    void BuildWorld()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "Ground"; g.isStatic = true;
        g.transform.position = new Vector3(MW / 2f, 0, MH / 2f);
        g.transform.localScale = new Vector3(MW / 10f, 1, MH / 10f);
        g.GetComponent<Renderer>().material = matGround;

        MakePath(12, 4, 12, 42, 2.5f);
        MakePath(4, 20, 50, 20, 2f);
        MakePath(25, 20, 25, 36, 2f);
        MakePath(43, 14, 43, 36, 2f);
        MakePath(35, 9, 50, 9, 2f);
        MakePath(25, 36, 45, 60, 2f);

        var wp = new GameObject("Walls").transform;
        for (int x = 0; x < MW; x++)
            for (int z = 0; z < MH; z++)
                if (map[x, z] == 1)
                {
                    bool border = x <= 1 || x >= MW - 2 || z <= 1 || z >= MH - 2;
                    float h = border ? 4.5f : 3.2f;
                    var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    w.transform.SetParent(wp); w.isStatic = true;
                    w.transform.position = new Vector3(x + .5f, h / 2f, z + .5f);
                    w.transform.localScale = new Vector3(1, h, 1);
                    w.GetComponent<Renderer>().material = border ? matStoneDark : matWallExt;
                }

        // Telhados sobre as casas
        AddRoof(8, 8, 16, 14, 3.2f);
        AddRoof(6, 40, 18, 50, 3.2f);
        AddRoof(38, 22, 48, 30, 3.2f);
        AddRoof(30, 6, 40, 12, 3.2f);
        AddRoof(22, 28, 32, 36, 3.2f);
        AddRoof(42, 55, 56, 65, 3.2f);
    }

    void MakePath(float x1, float z1, float x2, float z2, float w)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = "Path"; p.isStatic = true;
        p.transform.position = new Vector3((x1 + x2) / 2f, .01f, (z1 + z2) / 2f);
        p.transform.localScale = new Vector3(Mathf.Abs(x2 - x1) + w, .02f, Mathf.Abs(z2 - z1) + w);
        p.GetComponent<Renderer>().material = matPath;
        Destroy(p.GetComponent<Collider>());
    }

    void AddRoof(int x1, int z1, int x2, int z2, float wallH)
    {
        float cx = (x1 + x2) / 2f + .5f, cz = (z1 + z2) / 2f + .5f;
        float sx = x2 - x1 + 1.5f, sz = z2 - z1 + 1.5f;
        // Base do telhado
        var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
        r.isStatic = true; r.name = "Roof";
        r.transform.position = new Vector3(cx, wallH + .4f, cz);
        r.transform.localScale = new Vector3(sx, .15f, sz);
        r.GetComponent<Renderer>().material = matRoof;
        Destroy(r.GetComponent<Collider>());
        // Cumeeira
        var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ridge.isStatic = true; ridge.name = "Ridge";
        ridge.transform.position = new Vector3(cx, wallH + 1.2f, cz);
        ridge.transform.localScale = new Vector3(sx * .15f, .6f, sz + .2f);
        ridge.GetComponent<Renderer>().material = matRoof;
        Destroy(ridge.GetComponent<Collider>());
    }

    // ====================================================================
    // CENÁRIO COMPLETO
    // ====================================================================
    void BuildScenery()
    {
        var sp = new GameObject("Scenery").transform;
        var rng = new System.Random(42);
        for (int i = 0; i < 35; i++) PlaceBarrel(sp, rng);
        for (int i = 0; i < 20; i++) PlaceCrate(sp, rng);
        for (int i = 0; i < 28; i++) PlaceTree(sp, rng);
        for (int i = 0; i < 22; i++) PlaceTorch(sp, rng);
        for (int i = 0; i < 8; i++)  PlaceWell(sp, rng);
        for (int i = 0; i < 18; i++) PlaceHay(sp, rng);
        for (int i = 0; i < 14; i++) PlaceFence(sp, rng);
        for (int i = 0; i < 6; i++)  PlaceCart(sp, rng);
        for (int i = 0; i < 14; i++) PlaceGrave(sp, rng);
        for (int i = 0; i < 10; i++) PlaceLamp(sp, rng);
    }

    Vector3 Rnd(System.Random r) { for (int a = 0; a < 80; a++) { int x = r.Next(3, MW - 3), z = r.Next(3, MH - 3); if (map[x, z] == 0) return new Vector3(x + .5f, 0, z + .5f); } return Vector3.zero; }
    Vector3 RndArea(System.Random r, int x1, int z1, int x2, int z2) { for (int a = 0; a < 30; a++) { int x = r.Next(x1, x2), z = r.Next(z1, z2); if (map[x, z] == 0) return new Vector3(x + .5f, 0, z + .5f); } return Vector3.zero; }

    void PlaceBarrel(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var root = new GameObject("Barrel");
        root.transform.SetParent(p);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, r.Next(0, 360), 0);
        root.AddComponent<BreakableProp>().Init(BreakableProp.PropType.Barrel, 24f, .55f);

        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        b.transform.SetParent(root.transform, false);
        b.transform.localPosition = Vector3.up * .5f;
        b.transform.localScale = new Vector3(.4f, .5f, .4f);
        b.GetComponent<Renderer>().material = matWood;
        for (int i = 0; i < 3; i++)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * (.15f + i * .35f);
            ring.transform.localScale = new Vector3(.44f, .015f, .44f);
            ring.GetComponent<Renderer>().material = matMetal;
            Destroy(ring.GetComponent<Collider>());
        }
    }

    void PlaceCrate(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var root = new GameObject("Crate");
        root.transform.SetParent(p);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, r.Next(0, 360), 0);
        root.AddComponent<BreakableProp>().Init(BreakableProp.PropType.Crate, 18f, .65f);

        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(root.transform, false);
        c.transform.localPosition = Vector3.up * .35f;
        c.transform.localScale = Vector3.one * .7f;
        c.GetComponent<Renderer>().material = matWood;
    }

    void PlaceTree(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(p); trunk.isStatic = true;
        trunk.transform.position = pos + Vector3.up * 2f;
        trunk.transform.localScale = new Vector3(.2f, 2f, .2f);
        trunk.GetComponent<Renderer>().material = matTrunk;
        int layers = r.Next(3, 5);
        for (int j = 0; j < layers; j++)
        {
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaf.transform.SetParent(p); leaf.isStatic = true;
            float fy = 4f + j * .6f, fs = 2.8f - j * .6f;
            leaf.transform.position = pos + new Vector3((float)(r.NextDouble() - .5) * .3f, fy, (float)(r.NextDouble() - .5) * .3f);
            leaf.transform.localScale = new Vector3(fs, fs * .7f, fs);
            byte gr = (byte)(32 + j * 8 + r.Next(-5, 5));
            byte rd = (byte)(30 + j * 6 + r.Next(0, 18));
            leaf.GetComponent<Renderer>().material = Mat(new Color32(rd, gr, (byte)(10 + j * 3), 255));
            Destroy(leaf.GetComponent<Collider>());
        }
    }

    void PlaceTorch(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.transform.SetParent(p); post.isStatic = true;
        post.transform.position = pos + Vector3.up * .9f;
        post.transform.localScale = new Vector3(.08f, 1.8f, .08f);
        post.GetComponent<Renderer>().material = matWoodDark;
        var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.transform.SetParent(p);
        flame.transform.position = pos + Vector3.up * 1.9f;
        flame.transform.localScale = new Vector3(.12f, .22f, .12f);
        flame.GetComponent<Renderer>().material = MatEmissive(new Color32(255, 140, 30, 255), new Color(3f, 1.2f, .2f));
        Destroy(flame.GetComponent<Collider>());
        var lo = new GameObject("TL"); lo.transform.SetParent(p); lo.transform.position = pos + Vector3.up * 2f;
        var pl = lo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1, .55f, .15f); pl.range = 8; pl.intensity = 1.8f; pl.shadows = LightShadows.Soft;
        lo.AddComponent<TorchFlicker>();
    }

    void PlaceWell(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wall.transform.SetParent(p); wall.isStatic = true;
        wall.transform.position = pos + Vector3.up * .45f;
        wall.transform.localScale = new Vector3(.9f, .45f, .9f);
        wall.GetComponent<Renderer>().material = matStone;
        for (int s = -1; s <= 1; s += 2)
        {
            var pp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pp.transform.SetParent(p); pp.isStatic = true;
            pp.transform.position = pos + new Vector3(s * .5f, .95f, 0);
            pp.transform.localScale = new Vector3(.06f, 1f, .06f);
            pp.GetComponent<Renderer>().material = matWoodDark;
            Destroy(pp.GetComponent<Collider>());
        }
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(p); roof.isStatic = true;
        roof.transform.position = pos + Vector3.up * 1.5f;
        roof.transform.localScale = new Vector3(1.3f, .06f, .5f);
        roof.GetComponent<Renderer>().material = matWoodDark;
        Destroy(roof.GetComponent<Collider>());
    }

    void PlaceHay(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
        h.transform.SetParent(p); h.isStatic = true;
        h.transform.position = pos + Vector3.up * .4f;
        h.transform.localScale = new Vector3(1.2f, .8f, .8f);
        h.transform.rotation = Quaternion.Euler(0, r.Next(0, 360), 0);
        h.GetComponent<Renderer>().material = Mat(new Color32(165, 145, 55, 255));
    }

    void PlaceFence(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var root = new GameObject("Fence"); root.transform.SetParent(p); root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, r.Next(0, 4) * 90, 0);
        for (int i = 0; i < 3; i++)
        {
            var fp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fp.transform.SetParent(root.transform); fp.isStatic = true;
            fp.transform.localPosition = new Vector3(i * 1.2f - 1.2f, .5f, 0);
            fp.transform.localScale = new Vector3(.08f, 1, .08f);
            fp.GetComponent<Renderer>().material = matFence;
        }
        for (int i = 0; i < 2; i++)
        {
            var pl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pl.transform.SetParent(root.transform); pl.isStatic = true;
            pl.transform.localPosition = new Vector3(0, .3f + i * .4f, 0);
            pl.transform.localScale = new Vector3(2.4f, .06f, .04f);
            pl.GetComponent<Renderer>().material = matFence;
            Destroy(pl.GetComponent<Collider>());
        }
    }

    void PlaceCart(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var root = new GameObject("Cart"); root.transform.SetParent(p); root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, r.Next(0, 360), 0);
        var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plat.transform.SetParent(root.transform); plat.isStatic = true;
        plat.transform.localPosition = new Vector3(0, .5f, 0);
        plat.transform.localScale = new Vector3(1.8f, .1f, 1f);
        plat.GetComponent<Renderer>().material = matWood;
        for (int s = -1; s <= 1; s += 2)
        {
            var wh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wh.transform.SetParent(root.transform); wh.isStatic = true;
            wh.transform.localPosition = new Vector3(s * .8f, .3f, 0);
            wh.transform.localRotation = Quaternion.Euler(0, 0, 90);
            wh.transform.localScale = new Vector3(.6f, .04f, .6f);
            wh.GetComponent<Renderer>().material = matWoodDark;
            Destroy(wh.GetComponent<Collider>());
        }
    }

    void PlaceGrave(Transform p, System.Random r)
    {
        var pos = RndArea(r, 51, 46, 61, 57); if (pos == Vector3.zero) return;
        var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
        s.transform.SetParent(p); s.isStatic = true;
        float h = .5f + (float)r.NextDouble() * .4f;
        s.transform.position = pos + Vector3.up * h / 2f;
        s.transform.localScale = new Vector3(.5f, h, .12f);
        s.transform.rotation = Quaternion.Euler(r.Next(-5, 5), r.Next(-15, 15), r.Next(-3, 3));
        s.GetComponent<Renderer>().material = matStoneDark;
    }

    void PlaceLamp(Transform p, System.Random r)
    {
        var pos = Rnd(r); if (pos == Vector3.zero) return;
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(p); post.isStatic = true;
        post.transform.position = pos + Vector3.up * 1.6f;
        post.transform.localScale = new Vector3(.06f, 1.6f, .06f);
        post.GetComponent<Renderer>().material = matMetal;
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lamp.transform.SetParent(p); lamp.isStatic = true;
        lamp.transform.position = pos + Vector3.up * 3.3f;
        lamp.transform.localScale = Vector3.one * .25f;
        lamp.GetComponent<Renderer>().material = MatEmissive(new Color32(255, 200, 100, 200), new Color(2, 1.2f, .4f));
        Destroy(lamp.GetComponent<Collider>());
        var lo = new GameObject("LL"); lo.transform.SetParent(p); lo.transform.position = pos + Vector3.up * 3.2f;
        var pl = lo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1, .8f, .4f); pl.range = 10; pl.intensity = 1.2f; pl.shadows = LightShadows.Soft;
    }

    // ====================================================================
    // FOGUEIRA CENTRAL
    // ====================================================================
    void BuildBonfire()
    {
        Vector3 pos = new Vector3(25, 0, 25);
        var parent = new GameObject("Bonfire").transform;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45 * Mathf.Deg2Rad;
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.transform.SetParent(parent); s.isStatic = true;
            s.transform.position = pos + new Vector3(Mathf.Cos(a) * .8f, .1f, Mathf.Sin(a) * .8f);
            s.transform.localScale = new Vector3(.25f, .2f, .25f);
            s.GetComponent<Renderer>().material = matStoneDark; Destroy(s.GetComponent<Collider>());
        }
        for (int i = 0; i < 4; i++)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.transform.SetParent(parent); log.isStatic = true;
            log.transform.position = pos + Vector3.up * .15f;
            log.transform.rotation = Quaternion.Euler(80, i * 45, 0);
            log.transform.localScale = new Vector3(.08f, .4f, .08f);
            log.GetComponent<Renderer>().material = matTrunk; Destroy(log.GetComponent<Collider>());
        }
        for (int i = 0; i < 3; i++)
        {
            var fl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fl.transform.SetParent(parent);
            fl.transform.position = pos + new Vector3((i - 1) * .12f, .4f + i * .15f, 0);
            float fs = .25f - i * .06f;
            fl.transform.localScale = new Vector3(fs, fs * 1.5f, fs);
            fl.GetComponent<Renderer>().material = MatEmissive(new Color32(255, (byte)(100 + i * 40), 20, 255), new Color(4 - i, 1.5f - i * .3f, .1f));
            Destroy(fl.GetComponent<Collider>());
        }
        var lo = new GameObject("BFL"); lo.transform.SetParent(parent); lo.transform.position = pos + Vector3.up * 1.5f;
        var pl = lo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1, .5f, .1f); pl.range = 16; pl.intensity = 2.5f; pl.shadows = LightShadows.Soft;
        lo.AddComponent<TorchFlicker>();
    }

    // ====================================================================
    // IGREJA
    // ====================================================================
    void BuildChurch()
    {
        Vector3 pos = new Vector3(55, 0, 15);
        var parent = new GameObject("Church").transform;
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(parent); body.isStatic = true;
        body.transform.position = pos + Vector3.up * 2.5f;
        body.transform.localScale = new Vector3(8, 5, 6);
        body.GetComponent<Renderer>().material = matWallExt;
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.SetParent(parent); roof.isStatic = true;
        roof.transform.position = pos + Vector3.up * 5.5f;
        roof.transform.localScale = new Vector3(9, 1, 7);
        roof.GetComponent<Renderer>().material = matRoof; Destroy(roof.GetComponent<Collider>());
        var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tower.transform.SetParent(parent); tower.isStatic = true;
        tower.transform.position = pos + new Vector3(0, 6, -2);
        tower.transform.localScale = new Vector3(2.5f, 4, 2.5f);
        tower.GetComponent<Renderer>().material = matStoneDark;
        var cv = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cv.transform.SetParent(parent); cv.isStatic = true;
        cv.transform.position = pos + new Vector3(0, 9, -2);
        cv.transform.localScale = new Vector3(.15f, 1.2f, .15f);
        cv.GetComponent<Renderer>().material = matMetal; Destroy(cv.GetComponent<Collider>());
        var ch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ch.transform.SetParent(parent); ch.isStatic = true;
        ch.transform.position = pos + new Vector3(0, 8.7f, -2);
        ch.transform.localScale = new Vector3(.6f, .12f, .12f);
        ch.GetComponent<Renderer>().material = matMetal; Destroy(ch.GetComponent<Collider>());
        // Sino
        var bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bell.transform.SetParent(parent); bell.isStatic = true;
        bell.transform.position = pos + new Vector3(0, 7, -2);
        bell.transform.localScale = new Vector3(.5f, .6f, .5f);
        bell.GetComponent<Renderer>().material = Mat(new Color32(140, 120, 40, 255));
        Destroy(bell.GetComponent<Collider>());
    }

    // ====================================================================
    // MERCHANT
    // ====================================================================
    void BuildMerchant()
    {
        merchantObj = new GameObject("Merchant"); merchantObj.transform.position = merchantPos;
        var cloak = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cloak.transform.SetParent(merchantObj.transform);
        cloak.transform.localPosition = Vector3.up;
        cloak.transform.localScale = new Vector3(.7f, 1, .5f);
        cloak.GetComponent<Renderer>().material = Mat(new Color32(50, 22, 75, 255));
        var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hood.transform.SetParent(merchantObj.transform);
        hood.transform.localPosition = Vector3.up * 2.1f;
        hood.transform.localScale = new Vector3(.45f, .5f, .45f);
        hood.GetComponent<Renderer>().material = Mat(new Color32(40, 18, 60, 255));
        for (int s = -1; s <= 1; s += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.transform.SetParent(merchantObj.transform);
            eye.transform.localPosition = new Vector3(s * .08f, 2.15f, .18f);
            eye.transform.localScale = Vector3.one * .06f;
            eye.GetComponent<Renderer>().material = MatEmissive(new Color32(255, 200, 80, 255), new Color(2, 1.5f, .3f));
            Destroy(eye.GetComponent<Collider>());
        }
        var carpet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carpet.transform.SetParent(merchantObj.transform);
        carpet.transform.localPosition = new Vector3(0, .02f, 1.2f);
        carpet.transform.localScale = new Vector3(2, .04f, 1);
        carpet.GetComponent<Renderer>().material = Mat(new Color32(100, 40, 30, 255));
        Destroy(carpet.GetComponent<Collider>());
        var lo = new GameObject("ML"); lo.transform.SetParent(merchantObj.transform); lo.transform.localPosition = Vector3.up * 2.5f;
        var pl = lo.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(.8f, .5f, 1); pl.range = 6; pl.intensity = 1.5f;
        merchantObj.SetActive(false);
    }

    void BuildBlueMedallionRequest()
    {
        medallionParent = new GameObject("BlueMedallions").transform;

        blueNoticeObj = new GameObject("BlueNotice");
        blueNoticeObj.transform.position = merchantPos + new Vector3(-1.5f, 1.4f, 1.6f);
        var notice = blueNoticeObj.AddComponent<BlueMedallionNotice>();
        notice.message = "Blue medallions request started.";

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.transform.SetParent(blueNoticeObj.transform, false);
        board.transform.localScale = new Vector3(.45f, .3f, .03f);
        board.GetComponent<Renderer>().material = Mat(new Color32(80, 100, 165, 255));

        SpawnBlueMedallion(0, new Vector3(10f, 2.8f, 11f));
        SpawnBlueMedallion(1, new Vector3(15f, 3.3f, 9f));
        SpawnBlueMedallion(2, new Vector3(13f, 3f, 43f));
        SpawnBlueMedallion(3, new Vector3(9f, 2.6f, 48f));
        SpawnBlueMedallion(4, new Vector3(22f, 2.7f, 21f));
        SpawnBlueMedallion(5, new Vector3(29f, 3f, 19f));
        SpawnBlueMedallion(6, new Vector3(26f, 2.4f, 34f));
        SpawnBlueMedallion(7, new Vector3(35f, 2.9f, 10f));
        SpawnBlueMedallion(8, new Vector3(41f, 3.1f, 24f));
        SpawnBlueMedallion(9, new Vector3(46f, 2.8f, 28f));
        SpawnBlueMedallion(10, new Vector3(48f, 3f, 58f));
        SpawnBlueMedallion(11, new Vector3(56f, 3.1f, 54f));
        SpawnBlueMedallion(12, new Vector3(55f, 4.2f, 15f));
        SpawnBlueMedallion(13, new Vector3(60f, 2.7f, 50f));
        SpawnBlueMedallion(14, new Vector3(18f, 2.5f, 25f));
    }

    void SpawnBlueMedallion(int index, Vector3 pos)
    {
        var medallion = new GameObject("BlueMedallion");
        medallion.transform.SetParent(medallionParent);
        medallion.transform.position = pos;
        medallion.AddComponent<BlueMedallion>().Init(index);
        if (index >= 0 && index < blueMedallionFlags.Length && blueMedallionFlags[index])
            medallion.SetActive(false);

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plate.transform.SetParent(medallion.transform, false);
        plate.transform.localRotation = Quaternion.Euler(90, 0, 0);
        plate.transform.localScale = new Vector3(.3f, .015f, .3f);
        plate.GetComponent<Renderer>().material = MatEmissive(new Color32(60, 120, 230, 255), new Color(.2f, .45f, 1.2f));

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(medallion.transform, false);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ring.transform.localScale = new Vector3(.34f, .01f, .34f);
        ring.GetComponent<Renderer>().material = matMetal;
        Destroy(ring.GetComponent<Collider>());
    }

    // ====================================================================
    // ILUMINAÇÃO
    // ====================================================================
    void SetupLighting()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) Destroy(l.gameObject);
        var sun = new GameObject("Sun"); sun.transform.rotation = Quaternion.Euler(35, -45, 0);
        var dl = sun.AddComponent<Light>(); dl.type = LightType.Directional;
        dl.color = new Color(.52f, .48f, .4f); dl.intensity = .4f;
        dl.shadows = LightShadows.Soft; dl.shadowStrength = .75f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.1f, .08f, .06f);
        RenderSettings.ambientEquatorColor = new Color(.07f, .05f, .03f);
        RenderSettings.ambientGroundColor = new Color(.03f, .02f, .015f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color32(38, 34, 28, 255);
        RenderSettings.fogDensity = .02f;
    }

    void SpawnPlayer()
    {
        var o = new GameObject("Player"); o.transform.position = playerStart;
        player = o.AddComponent<Player>();
    }

    void SpawnEnvironmentOverrides()
    {
        var overlay = VisualOverrideLoader.InstantiateWorldPrefab(
            "Overrides/VillageOverlay",
            new Vector3(MW * .5f, 0, MH * .5f),
            Vector3.zero,
            Vector3.one);

        if (overlay)
            overlay.name = "VillageOverlay";
    }

    // ====================================================================
    // UPDATE
    // ====================================================================
    void Update()
    {
        SyncAudioState();
        if (State == GState.Title)
        { if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape)) { State = GState.Playing; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; StartWave(); } return; }
        if (State == GState.Dead)
        { if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
        if (State == GState.WaveIntro) { waveIntroTimer -= Time.deltaTime; if (waveIntroTimer <= 0) State = GState.Playing; return; }
        if (State == GState.Playing && enemiesAlive <= 0 && wave > 0)
        {
            CompleteWaveRewards();
            State = GState.Merchant;
            merchantObj.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (SaveSystem.I) SaveSystem.I.Save(0, true);
            if (ui) ui.ShowStatusMessage("AREA CLEAR - MERCHANT OPEN", new Color32(215, 185, 90, 255), 2.4f);
        }

    }

    public void CloseMerchant()
    {
        merchantObj.SetActive(false);
        State = GState.Playing;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (ui) ui.ShowStatusMessage("GET READY...", new Color32(220, 165, 90, 255), 1.2f);
        StartWave();
    }

    public void ActivateBlueMedallionRequest()
    {
        blueNoticeCollected = true;
        blueRequestActive = true;
        if (blueNoticeObj) blueNoticeObj.SetActive(false);
        if (ui)
            ui.ShowStatusMessage("BLUE MEDALLIONS REQUEST STARTED", new Color32(80, 150, 255, 255), 2f);
    }

    public void OnBlueMedallionDestroyed(int index)
    {
        if (index >= 0 && index < blueMedallionFlags.Length && !blueMedallionFlags[index])
        {
            blueMedallionFlags[index] = true;
            blueMedallionsDestroyed = Mathf.Clamp(blueMedallionsDestroyed + 1, 0, BLUE_MEDALLION_TOTAL);
        }

        if (!blueRequestActive)
            blueRequestActive = true;

        if (ui)
            ui.ShowStatusMessage($"BLUE MEDALLION {blueMedallionsDestroyed}/{BLUE_MEDALLION_TOTAL}", new Color32(80, 150, 255, 255), 1.25f);

        if (blueMedallionsDestroyed >= BLUE_MEDALLION_TOTAL && ui)
            ui.ShowStatusMessage("ALL BLUE MEDALLIONS DESTROYED", new Color32(95, 205, 255, 255), 2f);
    }

    public bool CanClaimPunisherReward()
    {
        return blueRequestActive && blueMedallionsDestroyed >= 10 && !blueRewardClaimed && player;
    }

    public string GetBlueMedallionStatusText()
    {
        if (!blueRequestActive && !blueNoticeCollected)
            return "Find the blue request note near the merchant.";
        if (blueRewardClaimed)
            return "Punisher reward claimed.";
        if (blueMedallionsDestroyed >= BLUE_MEDALLION_TOTAL)
            return "All medallions destroyed. Return to the merchant.";
        return $"Blue Medallions: {blueMedallionsDestroyed}/{BLUE_MEDALLION_TOTAL}";
    }

    public string GetBlueMedallionRewardLabel()
    {
        return blueMedallionsDestroyed >= BLUE_MEDALLION_TOTAL ? "Claim Punisher + Special Pierce" : "Claim Punisher";
    }

    public string GetBlueMedallionRewardDetail()
    {
        return blueMedallionsDestroyed >= BLUE_MEDALLION_TOTAL ?
            "Original request reward with the full piercing bonus." :
            "Unlocks the Punisher handgun variant.";
    }

    public bool ClaimPunisherReward()
    {
        if (!CanClaimPunisherReward() || !player) return false;

        bool special = blueMedallionsDestroyed >= BLUE_MEDALLION_TOTAL;
        if (!player.UnlockPunisher(special)) return false;

        blueRewardClaimed = true;
        if (ui)
            ui.ShowStatusMessage(special ? "PUNISHER WITH SPECIAL PIERCE ACQUIRED" : "PUNISHER ACQUIRED",
                new Color32(95, 205, 255, 255), 2f);
        return true;
    }

    public bool[] GetBlueMedallionFlagsCopy()
    {
        var copy = new bool[blueMedallionFlags.Length];
        blueMedallionFlags.CopyTo(copy, 0);
        return copy;
    }

    public void ApplyBlueMedallionState(bool noteCollected, bool requestActive, int destroyed, bool rewardClaimed, bool[] flags)
    {
        blueNoticeCollected = noteCollected;
        blueRequestActive = requestActive;
        blueRewardClaimed = rewardClaimed;
        blueMedallionsDestroyed = Mathf.Clamp(destroyed, 0, BLUE_MEDALLION_TOTAL);

        for (int i = 0; i < blueMedallionFlags.Length; i++)
            blueMedallionFlags[i] = flags != null && i < flags.Length && flags[i];

        if (blueNoticeObj)
            blueNoticeObj.SetActive(!blueNoticeCollected);
        if (!medallionParent) return;

        for (int i = 0; i < medallionParent.childCount && i < blueMedallionFlags.Length; i++)
            medallionParent.GetChild(i).gameObject.SetActive(!blueMedallionFlags[i]);
    }

    int EnemyCountForWave(int waveIndex) => 4 + waveIndex * 3;

    void StartWave(int forcedWave = -1, int forcedCount = -1)
    {
        wave = forcedWave > 0 ? forcedWave : wave + 1;
        waveIntroTimer = forcedWave > 0 ? 1.2f : 2.5f;
        State = GState.WaveIntro;
        ClearActiveEnemies();
        waveRewardGranted = false;
        merchantDiscountPercent = 0;
        lastWaveBonus = 0;
        lastWaveAccuracy = 0f;
        lastWavePerfect = false;
        if (player) player.BeginWaveStats();

        int count = forcedCount > 0 ? forcedCount : EnemyCountForWave(wave);
        enemiesAlive = count;
        if (ui) ui.ShowStatusMessage($"WAVE {wave} START", new Color32(225, 200, 105, 255), 2f);
        for (int i = 0; i < count; i++)
        {
            if (spawnPts.Count == 0) break;
            Vector3 sp = spawnPts[Random.Range(0, spawnPts.Count)];
            int safe = 30;
            while (player && Vector3.Distance(sp, player.transform.position) < 14 && safe-- > 0)
                sp = spawnPts[Random.Range(0, spawnPts.Count)];
            SpawnEnemy(sp);
        }
    }

    void SpawnEnemy(Vector3 pos)
    {
        var o = new GameObject("Ganado"); o.transform.SetParent(enemyParent); o.transform.position = pos;
        var e = o.AddComponent<Enemy>();
        Enemy.EType t = Enemy.EType.Villager;
        float roll = Random.value;
        if (wave >= 5 && roll < .08f) t = Enemy.EType.Chainsaw;
        else if (wave >= 3 && roll < .2f) t = Enemy.EType.Heavy;
        else if (wave >= 2 && roll < .4f) t = Enemy.EType.Pitchfork;
        e.Init(t, alertedOnSpawn: true);
    }

    public void OnEnemyDied(Vector3 pos, Enemy.EType type)
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1); kills++;

        bool richDrop = type == Enemy.EType.Chainsaw || type == Enemy.EType.Heavy;
        float dropChance = type == Enemy.EType.Chainsaw ? .95f :
                           type == Enemy.EType.Heavy ? .55f : .4f;
        if (Random.value < dropChance)
        {
            var pk = new GameObject("Pickup");
            pk.transform.position = pos + Vector3.up * .3f;
            pk.AddComponent<Pickup>().Init(richDrop);
        }

        if (type == Enemy.EType.Heavy && Random.value < .35f)
        {
            var moneyDrop = new GameObject("PesetasPickup");
            moneyDrop.transform.position = pos + new Vector3(.18f, .3f, -.12f);
            moneyDrop.AddComponent<Pickup>().InitMoney(Random.Range(420, 760));
        }

        if (type == Enemy.EType.Chainsaw)
        {
            var moneyDrop = new GameObject("PesetasPickup");
            moneyDrop.transform.position = pos + new Vector3(-.15f, .3f, .1f);
            moneyDrop.AddComponent<Pickup>().InitMoney(Random.Range(1200, 2200));

            var treasure = new GameObject("TreasurePickup");
            treasure.transform.position = pos + Vector3.up * .45f;
            var pickup = treasure.AddComponent<Pickup>();
            pickup.InitTreasure(Random.value < .7f ? Pickup.TreasureType.Ruby : Pickup.TreasureType.Pendant);
        }
        if (type == Enemy.EType.Chainsaw && ui)
            ui.ShowStatusMessage("CHAINSAW THREAT ELIMINATED", new Color32(220, 80, 70, 255), 1.8f);
    }

    void CompleteWaveRewards()
    {
        if (waveRewardGranted || !player) return;

        waveRewardGranted = true;
        lastWaveAccuracy = player.GetWaveAccuracy();
        lastWavePerfect = player.GetWaveDamageTaken() <= .01f;

        int baseReward = 180 + wave * 90;
        int accuracyReward = lastWaveAccuracy >= .8f ? 220 + wave * 30 :
                             lastWaveAccuracy >= .6f ? 120 + wave * 20 : 0;
        int perfectReward = lastWavePerfect ? 180 + wave * 45 : 0;

        lastWaveBonus = baseReward + accuracyReward + perfectReward;
        player.money += lastWaveBonus;

        merchantDiscountPercent = Mathf.Clamp((wave - 1) * 2 + (lastWaveAccuracy >= .75f ? 4 : 0) + (lastWavePerfect ? 6 : 0), 0, 24);

        if (ui)
        {
            string label = $"WAVE BONUS +{lastWaveBonus}";
            if (lastWavePerfect) label += "  PERFECT";
            ui.ShowStatusMessage(label, new Color32(225, 200, 105, 255), 2.2f);
        }
    }

    public void RestoreProgress(int savedWave, int savedKills, int enemiesRemaining, GState savedState,
        int savedDiscount = 0, int savedBonus = 0, float savedAccuracy = 0f, bool savedPerfect = false)
    {
        kills = Mathf.Max(0, savedKills);
        merchantDiscountPercent = Mathf.Clamp(savedDiscount, 0, 24);
        lastWaveBonus = Mathf.Max(0, savedBonus);
        lastWaveAccuracy = Mathf.Clamp01(savedAccuracy);
        lastWavePerfect = savedPerfect;
        merchantObj.SetActive(false);
        ClearActiveEnemies();

        if (savedWave <= 0)
        {
            wave = 0;
            enemiesAlive = 0;
            State = GState.Playing;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        if (savedState == GState.Merchant || enemiesRemaining <= 0)
        {
            wave = savedWave;
            enemiesAlive = 0;
            waveRewardGranted = true;
            State = GState.Merchant;
            merchantObj.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (ui) ui.ShowStatusMessage("MERCHANT RESTORED", new Color32(215, 185, 90, 255), 1.8f);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartWave(savedWave, enemiesRemaining);
    }

    public int GetMerchantPrice(int basePrice)
    {
        float mult = 1f - merchantDiscountPercent / 100f;
        return Mathf.Max(50, Mathf.RoundToInt(basePrice * mult));
    }

    void ClearActiveEnemies()
    {
        if (!enemyParent) return;
        for (int i = enemyParent.childCount - 1; i >= 0; i--)
        {
            var enemy = enemyParent.GetChild(i).gameObject;
            enemy.SetActive(false);
            Destroy(enemy);
        }
    }

    void SyncAudioState()
    {
        if (!AudioManager.I || lastMusicState == State) return;

        lastMusicState = State;
        switch (State)
        {
            case GState.Title: AudioManager.I.PlayMusic(AudioManager.Music.Ambient); break;
            case GState.WaveIntro: AudioManager.I.PlayMusic(AudioManager.Music.WaveIntro); break;
            case GState.Playing: AudioManager.I.PlayMusic(enemiesAlive > 0 ? AudioManager.Music.Combat : AudioManager.Music.Ambient); break;
            case GState.Merchant: AudioManager.I.PlayMusic(AudioManager.Music.Merchant); break;
            case GState.Dead: AudioManager.I.PlayMusic(AudioManager.Music.Dead); break;
        }
    }

    public void PlayerDied() { State = GState.Dead; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    public bool IsOpen(int x, int z) => x >= 0 && x < MW && z >= 0 && z < MH && map[x, z] == 0;
}

// ── TorchFlicker ───────────────────────────────────────────────────────────
public class TorchFlicker : MonoBehaviour
{
    Light _l; float _base;
    void Start() { _l = GetComponent<Light>() ?? GetComponentInParent<Light>(); if (_l) _base = _l.intensity; }
    void Update() { if (_l) _l.intensity = _base + Mathf.Sin(Time.time * 8 + transform.position.x) * .3f + Mathf.Sin(Time.time * 13 + transform.position.z) * .2f; }
}
