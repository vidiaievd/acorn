#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using MagicWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Собирает первую играбельную заготовку: рельеф по детской карте map-1, раскраска земли,
/// лес, домики, мост, озеро, герой-волшебник и один раунд «собери кристаллы».
/// Меню: Magic World → Собрать первую заготовку. Запускается повторно (пересобирает мир).
/// </summary>
public static class BuildFirstDemo
{
    const string ScenePath = "Assets/_Project/Scenes/World1_Forest.unity";
    const string ArtDir = "Assets/_Project/Art";
    const float WorldSize = 200f, TerrainHeight = 40f, WaterY = 1.2f;
    const float MapW = 1200f, MapH = 896f; // размеры картинки map-1

    // пиксель карты → мировые координаты (Terrain лежит от -100 до +100)
    static Vector2 P(float px, float py) => new Vector2(-100f + 200f * px / MapW, 100f - 200f * py / MapH);

    static readonly Vector2[] River = { P(60,375), P(300,410), P(450,425), P(600,450), P(700,500), P(850,560), P(1000,600), P(1150,650) };
    static readonly Vector2[][] Paths =
    {
        // главная тропа: дом героя → мост → камни
        new[]{ P(240,775), P(330,745), P(450,700), P(470,640), P(520,580), P(560,520), P(630,470), P(690,430), P(740,380), P(700,330), P(600,310), P(480,290), P(440,250), P(520,215), P(610,200) },
        // к грибу
        new[]{ P(300,590), P(380,620), P(470,640) },
        // к озеру
        new[]{ P(740,380), P(850,440), P(930,440), P(900,330), P(920,290) },
        // к хижине
        new[]{ P(470,700), P(580,740), P(700,740) },
    };

    static readonly Vector2 Lake = P(950, 250);
    const float LakeA = 17.5f, LakeB = 10f;
    static readonly Vector2 Plateau = P(270, 270);
    const float PlateauR = 17f;
    static readonly Vector2 Bridge = P(630, 470);

    static readonly Vector2[] CrystalSpots = { P(500,495), P(665,610), P(585,690), P(610,300), P(880,340), P(560,165) };

    // ───────────────────────────── материалы и меши ─────────────────────────────
    static Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static Material Mat(string name, Color c, float smooth = 0.15f, Color? emission = null)
    {
        string path = $"{ArtDir}/Materials/{name}.mat";
        Directory.CreateDirectory($"{ArtDir}/Materials");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WaterMat()
    {
        var m = Mat("Water", new Color(0.18f, 0.55f, 0.95f, 0.62f), 0.95f);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    static Mesh MakeCone(string name, int sides, float r, float h, bool bipyramid = false)
    {
        string path = $"{ArtDir}/Meshes/{name}.asset";
        Directory.CreateDirectory($"{ArtDir}/Meshes");
        var verts = new List<Vector3>(); var tris = new List<int>();
        float y0 = bipyramid ? 0f : 0f, yTop = bipyramid ? h * 0.5f : h, yBot = bipyramid ? -h * 0.5f : 0f;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
            var p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r);
            var p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
            AddTri(verts, tris, new Vector3(0, yTop, 0), p1, p0);                       // верх
            if (bipyramid) AddTri(verts, tris, new Vector3(0, yBot, 0), p0, p1);        // низ
            else AddTri(verts, tris, new Vector3(0, 0, 0), p0, p1);                     // донышко
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(mesh, old); return old; }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
    static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2); }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject MeshObj(Mesh mesh, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, float yRot = 0)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(0, yRot, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    // ───────────────────────────── рельеф ─────────────────────────────
    static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

    static float DistToPolyline(Vector2 p, Vector2[] line)
    {
        float best = float.MaxValue;
        for (int i = 0; i < line.Length - 1; i++)
        {
            var a = line[i]; var b = line[i + 1]; var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
        }
        return best;
    }
    static float DistToPaths(Vector2 p) { float d = float.MaxValue; foreach (var l in Paths) d = Mathf.Min(d, DistToPolyline(p, l)); return d; }

    static float Hill(Vector2 p, Vector2 c, float r, float h) { float d = (p - c).magnitude / r; return h * Mathf.Exp(-d * d * 2.2f); }

    static float HeightAt(float x, float z)
    {
        var p = new Vector2(x, z);
        // мягкие холмики
        float n = Mathf.PerlinNoise(x * 0.035f + 40f, z * 0.035f + 40f) - 0.5f;
        float n2 = Mathf.PerlinNoise(x * 0.12f + 7f, z * 0.12f + 7f) - 0.5f;
        float h = 3.4f + n * 3.0f + n2 * 0.5f;

        // холмы с карты
        h += Hill(p, P(950, 110), 22f, 5f) + Hill(p, P(1080, 700), 22f, 5f) + Hill(p, P(450, 840), 22f, 3.5f)
           + Hill(p, P(630, 330), 9f, 1.6f) + Hill(p, P(120, 840), 16f, 3f) + Hill(p, P(100, 190), 14f, 3f) + Hill(p, P(560, 170), 22f, 2.5f);

        // ровные площадки: у дома героя, у моста
        float flatStart = 1f - Smooth(0f, 16f, (p - P(240, 760)).magnitude);
        float flatBridge = 1f - Smooth(8f, 24f, (p - Bridge).magnitude);
        h = Mathf.Lerp(h, 3.4f, Mathf.Max(flatStart, flatBridge));

        // скалистое плато с большим деревом (цифра 7)
        float dP = (p - Plateau).magnitude;
        float plateau = 1f - Smooth(PlateauR - 2f, PlateauR + 4f, dP);
        h += plateau * (12f + n2 * 2f);

        // озеро (цифра 9)
        float dl = Mathf.Sqrt(Mathf.Pow((x - Lake.x) / LakeA, 2) + Mathf.Pow((z - Lake.y) / LakeB, 2));
        h = Mathf.Lerp(0.2f, h, Smooth(0.65f, 1.35f, dl));

        // река
        float dr = DistToPolyline(p, River);
        h = Mathf.Lerp(0.1f, h, Smooth(2.2f, 7.5f, dr));
        return h;
    }

    static void SculptTerrain(Terrain terrain)
    {
        var td = terrain.terrainData;
        td.size = new Vector3(WorldSize, TerrainHeight, WorldSize);
        int res = td.heightmapResolution;
        var hm = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float wx = -100f + WorldSize * xi / (res - 1), wz = -100f + WorldSize * zi / (res - 1);
                hm[zi, xi] = HeightAt(wx, wz) / TerrainHeight;
            }
        td.SetHeights(0, 0, hm);
    }

    static Texture2D NoiseTex(string name, Color a, Color b, float scale, float detail)
    {
        string path = $"{ArtDir}/Terrain/{name}.png";
        Directory.CreateDirectory($"{ArtDir}/Terrain");
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // бесшовность: смешиваем шум со сдвигом на период
                float u = x / (float)N, v = y / (float)N;
                float n = Wrap(u, v, scale, 3f);
                float d = Wrap(u, v, scale * 4f, 17f);
                tex.SetPixel(x, y, Color.Lerp(a, b, Mathf.Clamp01(n * 0.7f + d * detail)));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static float Wrap(float u, float v, float s, float off)
    {
        float a = Mathf.PerlinNoise(u * s + off, v * s + off), b = Mathf.PerlinNoise((u - 1) * s + off, v * s + off);
        float c = Mathf.PerlinNoise(u * s + off, (v - 1) * s + off), d = Mathf.PerlinNoise((u - 1) * s + off, (v - 1) * s + off);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static TerrainLayer MakeLayer(string name, Color a, Color b, float tile)
    {
        string path = $"{ArtDir}/Terrain/{name}.terrainlayer";
        var tex = NoiseTex(name, a, b, 6f, 0.35f);
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
        l.diffuseTexture = tex; l.tileSize = new Vector2(tile, tile); l.smoothness = 0f;
        EditorUtility.SetDirty(l);
        return l;
    }

    static void PaintTerrain(Terrain terrain)
    {
        var td = terrain.terrainData;
        td.terrainLayers = new[]
        {
            MakeLayer("Grass", new Color(0.22f, 0.55f, 0.16f), new Color(0.42f, 0.74f, 0.22f), 7f),
            MakeLayer("Path",  new Color(0.62f, 0.47f, 0.28f), new Color(0.78f, 0.62f, 0.40f), 5f),
            MakeLayer("Sand",  new Color(0.84f, 0.76f, 0.5f),  new Color(0.94f, 0.88f, 0.64f), 5f),
            MakeLayer("Rock",  new Color(0.38f, 0.38f, 0.40f), new Color(0.62f, 0.60f, 0.58f), 9f),
        };
        int res = td.alphamapResolution;
        var map = new float[res, res, 4];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float u = xi / (float)(res - 1), v = zi / (float)(res - 1);
                float wx = -100f + WorldSize * u, wz = -100f + WorldSize * v;
                var p = new Vector2(wx, wz);
                float h = HeightAt(wx, wz);
                float slope = td.GetSteepness(u, v);
                float jitter = Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f) - 0.5f;

                float path = 1f - Smooth(1.0f, 2.2f, DistToPaths(p) + jitter * 0.9f);
                float sand = 1f - Smooth(1.55f, 2.35f, h + jitter * 0.3f);
                float rock = Mathf.Max(Smooth(28f, 42f, slope), 1f - Smooth(PlateauR - 4f, PlateauR + 1f, (p - Plateau).magnitude) > 0 ? 0f : 0f);
                float onPlateau = 1f - Smooth(PlateauR - 1f, PlateauR + 3f, (p - Plateau).magnitude);
                rock = Mathf.Max(rock, onPlateau > 0.5f && slope > 20f ? 1f : 0f);

                float grass = 1f;
                float wRock = rock, wSand = sand * (1f - rock), wPath = path * (1f - rock) * (1f - sand * 0.7f);
                float wGrass = Mathf.Max(0f, grass - wRock - wSand - wPath);
                float sum = wRock + wSand + wPath + wGrass;
                map[zi, xi, 0] = wGrass / sum; map[zi, xi, 1] = wPath / sum; map[zi, xi, 2] = wSand / sum; map[zi, xi, 3] = wRock / sum;
            }
        td.SetAlphamaps(0, 0, map);
    }

    // ───────────────────────────── мир ─────────────────────────────
    static Vector3 OnGround(Terrain t, Vector2 p, float up = 0f) => new Vector3(p.x, t.SampleHeight(new Vector3(p.x, 0, p.y)) + t.transform.position.y + up, p.y);

    static void BuildScenery(Terrain terrain, Transform root)
    {
        var trunk = Mat("Trunk", new Color(0.42f, 0.27f, 0.15f), 0.1f);
        var leaf1 = Mat("LeafLight", new Color(0.42f, 0.78f, 0.22f), 0.2f);
        var leaf2 = Mat("LeafDark", new Color(0.13f, 0.5f, 0.24f), 0.1f);
        var spiral = Mat("LeafSpiral", new Color(0.55f, 0.85f, 0.25f), 0.2f);
        var cone8 = MakeCone("Cone8", 8, 0.5f, 1f);
        var cone4 = MakeCone("Cone4", 4, 0.5f, 1f);

        // деревья: ёлки и «леденцовые» деревья как на детской карте
        var forest = new GameObject("Forest").transform; forest.SetParent(root, false);
        var rng = new System.Random(7);
        var avoid = new List<Vector2> { P(240,775), P(290,540), P(710,720), Bridge, P(740,350), P(960,380), P(270,200) };
        avoid.AddRange(CrystalSpots);
        int placed = 0;
        for (int tries = 0; tries < 6000 && placed < 260; tries++)
        {
            float x = (float)rng.NextDouble() * 190f - 95f, z = (float)rng.NextDouble() * 190f - 95f;
            var p = new Vector2(x, z);
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / 95f;
            float density = 0.18f + 0.82f * Smooth(0.45f, 0.95f, edge) + 0.35f * Mathf.PerlinNoise(x * 0.06f, z * 0.06f);
            if (rng.NextDouble() > density * 0.6f) continue;
            float h = HeightAt(x, z);
            if (h < 2.3f || (p - Plateau).magnitude < PlateauR + 5f && (p - Plateau).magnitude > PlateauR - 3f) continue;
            if ((p - Plateau).magnitude < PlateauR) continue;
            if (DistToPaths(p) < 4f || DistToPolyline(p, River) < 11f) continue;
            if (((p - Lake).x / (LakeA + 6f)) * ((p - Lake).x / (LakeA + 6f)) + ((p - Lake).y / (LakeB + 6f)) * ((p - Lake).y / (LakeB + 6f)) < 1f) continue;
            if (terrain.terrainData.GetSteepness((x + 100f) / 200f, (z + 100f) / 200f) > 28f) continue;
            bool near = false; foreach (var a in avoid) if ((a - p).magnitude < 9f) { near = true; break; }
            if (near) continue;
            avoid.Add(p); // деревья не слипаются
            avoid[avoid.Count - 1] = p;
            float s = 0.8f + (float)rng.NextDouble() * 0.7f;
            var t = new GameObject(rng.NextDouble() < 0.55 ? "Pine" : "RoundTree").transform; t.SetParent(forest, false);
            t.position = OnGround(terrain, p); t.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            if (t.name == "Pine")
            {
                Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 0.9f * s, 0), new Vector3(0.45f * s, 0.9f * s, 0.45f * s), trunk).transform.localScale = new Vector3(0.45f * s, 0.9f * s, 0.45f * s);
                for (int k = 0; k < 3; k++)
                    MeshObj(cone8, "Layer", t, new Vector3(0, (1.2f + k * 1.5f) * s, 0), new Vector3((4.4f - k * 1.2f) * s, 2.8f * s, (4.4f - k * 1.2f) * s), k % 2 == 0 ? leaf2 : leaf1);
                RemoveColliders(t, "Trunk");
            }
            else
            {
                Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 1.4f * s, 0), new Vector3(0.5f * s, 1.4f * s, 0.5f * s), trunk);
                Prim(PrimitiveType.Sphere, "Crown", t, new Vector3(0, 3.9f * s, 0), new Vector3(3.4f * s, 3.4f * s, 3.4f * s), rng.NextDouble() < 0.5 ? leaf1 : spiral, false);
                RemoveColliders(t, "Trunk");
            }
            placed++;
        }

        var props = new GameObject("Props").transform; props.SetParent(root, false);
        var cream = Mat("Cream", new Color(0.97f, 0.88f, 0.7f), 0.1f);
        var wood = Mat("Wood", new Color(0.6f, 0.38f, 0.2f), 0.1f);
        var woodDark = Mat("WoodDark", new Color(0.4f, 0.24f, 0.13f), 0.1f);
        var red = Mat("Red", new Color(0.86f, 0.15f, 0.12f), 0.3f);
        var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.58f), 0.1f);
        var white = Mat("White", Color.white, 0.2f);

        // домик героя (1)
        var home = House(terrain, props, "HeroHouse", P(215, 740), cream, red, red, cone4, -25f, 1.2f);
        // хижина (справа внизу)
        House(terrain, props, "Hut", P(710, 715), wood, woodDark, wood, cone4, 10f, 1.2f);

        // гриб-домик (2)
        var mush = new GameObject("MushroomHouse").transform; mush.SetParent(props, false); mush.position = OnGround(terrain, P(290, 520));
        Prim(PrimitiveType.Cylinder, "Stem", mush, new Vector3(0, 2.2f, 0), new Vector3(3.4f, 2.2f, 3.4f), cream);
        var cap = Prim(PrimitiveType.Sphere, "Cap", mush, new Vector3(0, 4.6f, 0), new Vector3(8f, 4.6f, 8f), red);
        var rnd = new System.Random(3);
        for (int i = 0; i < 9; i++)
        {
            float a = i * 0.7f + 0.3f, el = 0.35f + (i % 3) * 0.25f;
            var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
            Prim(PrimitiveType.Sphere, "Spot", mush, new Vector3(0, 4.6f, 0) + new Vector3(dir.x * 4f, dir.y * 2.3f, dir.z * 4f), Vector3.one * 1.0f, white, false);
        }
        Prim(PrimitiveType.Cube, "Door", mush, new Vector3(0, 1.0f, 1.65f), new Vector3(1.3f, 2f, 0.2f), wood, false);

        // мост (5): через реку, поперёк течения
        BuildBridge(terrain, props, wood, woodDark);

        // большое дерево на плато (7)
        var big = new GameObject("BigTree").transform; big.SetParent(props, false);
        big.position = new Vector3(Plateau.x, HeightAt(Plateau.x, Plateau.y), Plateau.y);
        Prim(PrimitiveType.Cylinder, "Trunk", big, new Vector3(0, 3.5f, 0), new Vector3(2.2f, 3.5f, 2.2f), trunk);
        Prim(PrimitiveType.Sphere, "Crown", big, new Vector3(0, 9f, 0), new Vector3(10f, 7f, 10f), leaf1, false);
        Prim(PrimitiveType.Sphere, "Crown2", big, new Vector3(-3.2f, 8f, 1.5f), new Vector3(6f, 5f, 6f), leaf2, false);
        Prim(PrimitiveType.Sphere, "Crown3", big, new Vector3(3.2f, 8.5f, -1f), new Vector3(6.5f, 5.5f, 6.5f), spiral, false);

        // руины из камней (10)
        var ruins = new GameObject("Ruins").transform; ruins.SetParent(props, false);
        var rc = P(620, 175);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f; if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) < 25f) continue; // проход для тропы
            var pp = rc + new Vector2(Mathf.Cos(a) * 9f, Mathf.Sin(a) * 6f);
            float hs = 2.5f + (i % 3) * 1.1f;
            var rock = Prim(PrimitiveType.Cube, "RuinStone", ruins, OnGround(terrain, pp, hs * 0.45f), new Vector3(2.4f, hs, 1.8f), stone);
            rock.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), -a * Mathf.Rad2Deg + 90f, Random.Range(-8f, 8f));
        }
        // валуны (8) и у озера
        foreach (var bp in new[] { P(960, 380), P(1010, 400), P(920, 400), P(500, 395), P(210, 120), P(800, 150) })
        {
            float sz = 2.2f + (bp.x * 7f % 2f);
            Prim(PrimitiveType.Sphere, "Boulder", props, OnGround(terrain, bp, sz * 0.25f), new Vector3(sz * 1.3f, sz, sz * 1.1f), stone);
        }
        // пень с пещерой (6)
        var stump = new GameObject("CaveStump").transform; stump.SetParent(props, false); stump.position = OnGround(terrain, P(760, 350));
        Prim(PrimitiveType.Cylinder, "Stump", stump, new Vector3(0, 2f, 0), new Vector3(4f, 2f, 4f), trunk);
        Prim(PrimitiveType.Sphere, "Cave", stump, new Vector3(0, 1.1f, 1.9f), new Vector3(1.8f, 1.8f, 0.6f), Mat("Dark", new Color(0.05f, 0.03f, 0.03f), 0f), false);

        // цветы-ориентиры
        var flowerCols = new[] { Mat("FlowerPink", new Color(1f, 0.4f, 0.65f), 0.2f), Mat("FlowerBlue", new Color(0.35f, 0.5f, 1f), 0.2f), Mat("FlowerYellow", new Color(1f, 0.85f, 0.25f), 0.2f) };
        var fr = new System.Random(11);
        for (int i = 0; i < 160; i++)
        {
            var p = new Vector2((float)fr.NextDouble() * 180f - 90f, (float)fr.NextDouble() * 180f - 90f);
            if (HeightAt(p.x, p.y) < 2.5f || (p - Plateau).magnitude < PlateauR + 2f || DistToPaths(p) < 1.5f) continue;
            var f = Prim(PrimitiveType.Sphere, "Flower", props, OnGround(terrain, p, 0.28f), Vector3.one * 0.32f, flowerCols[i % 3], false);
        }
        StaticBatchingUtility.Combine(forest.gameObject);
    }

    static void RemoveColliders(Transform t, string keep)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>()) if (c.name != keep) Object.DestroyImmediate(c);
    }

    static Transform House(Terrain terrain, Transform parent, string name, Vector2 pos, Material wall, Material roofMat, Material doorMat, Mesh roofMesh, float yaw, float s)
    {
        var h = new GameObject(name).transform; h.SetParent(parent, false);
        h.position = OnGround(terrain, pos); h.rotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Walls", h, new Vector3(0, 1.6f, 0), new Vector3(5f, 3.2f, 4.4f), wall);
        MeshObj(roofMesh, "Roof", h, new Vector3(0, 3.2f, 0), new Vector3(8.2f, 2.6f, 7.0f), roofMat, 45f);
        Prim(PrimitiveType.Cube, "Door", h, new Vector3(0, 1.0f, 2.25f), new Vector3(1.2f, 2.0f, 0.15f), doorMat, false);
        var win = Mat("Window", new Color(0.7f, 0.85f, 1f), 0.8f, new Color(0.3f, 0.4f, 0.5f));
        Prim(PrimitiveType.Cube, "Window", h, new Vector3(1.6f, 1.9f, 2.25f), new Vector3(0.9f, 0.9f, 0.12f), win, false);
        Prim(PrimitiveType.Cube, "Window", h, new Vector3(-1.6f, 1.9f, 2.25f), new Vector3(0.9f, 0.9f, 0.12f), win, false);
        return h;
    }

    static void BuildBridge(Terrain terrain, Transform parent, Material wood, Material woodDark)
    {
        // направление реки у моста
        var along = (P(700, 500) - P(600, 450)).normalized;
        var across = new Vector2(-along.y, along.x);
        float yaw = Mathf.Atan2(across.x, across.y) * Mathf.Rad2Deg;
        var b = new GameObject("Bridge").transform; b.SetParent(parent, false);
        b.position = new Vector3(Bridge.x, 3.45f, Bridge.y); b.rotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cube, "Deck", b, new Vector3(0, -0.35f, 0), new Vector3(3.2f, 0.7f, 20f), wood);
        for (int i = -7; i <= 7; i++)
            Prim(PrimitiveType.Cube, "Plank", b, new Vector3(0, 0.02f, i * 1.1f), new Vector3(3.3f, 0.08f, 0.15f), woodDark, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Prim(PrimitiveType.Cube, "Rail", b, new Vector3(side * 1.55f, 0.9f, 0), new Vector3(0.14f, 0.14f, 17f), woodDark, false);
            for (int i = -2; i <= 2; i++)
                Prim(PrimitiveType.Cylinder, "Post", b, new Vector3(side * 1.55f, 0.5f, i * 3.6f), new Vector3(0.22f, 0.65f, 0.22f), woodDark, false);
        }
    }

    static void BuildWater(Transform root)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Plane); w.name = "Water";
        w.transform.SetParent(root, false); w.transform.position = new Vector3(0, WaterY, 0); w.transform.localScale = new Vector3(20, 1, 20);
        Object.DestroyImmediate(w.GetComponent<Collider>());
        var r = w.GetComponent<Renderer>(); r.sharedMaterial = WaterMat(); r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ───────────────────────────── герой и раунд ─────────────────────────────
    static HeroController BuildHero(Terrain terrain, Material spark, out Transform spawnPoint)
    {
        var robe = Mat("HeroRobe", new Color(0.35f, 0.3f, 0.85f), 0.2f);
        var hat = Mat("HeroHat", new Color(0.2f, 0.18f, 0.6f), 0.2f);
        var skin = Mat("HeroSkin", new Color(1f, 0.82f, 0.68f), 0.2f);
        var gold = Mat("HeroGold", new Color(1f, 0.8f, 0.2f), 0.5f);
        var orbMat = Mat("HeroOrb", new Color(0.5f, 0.95f, 1f), 0.9f, new Color(0.4f, 1.6f, 2.4f));
        var cone16 = MakeCone("Cone16", 16, 0.5f, 1f);

        var hero = new GameObject("Hero"); hero.tag = "Player";
        var spawn = P(235, 800);
        hero.transform.position = OnGround(terrain, spawn, 0.2f);
        var cc = hero.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.5f; cc.slopeLimit = 55f;
        var hc = hero.AddComponent<HeroController>();

        var model = new GameObject("Model").transform; model.SetParent(hero.transform, false);
        hc.model = model;
        // мантия — конус, голова — шар, шляпа — большой конус с полями
        MeshObj(cone16, "Robe", model, new Vector3(0, 0.0f, 0), new Vector3(1.25f, 1.2f, 1.25f), robe);
        Prim(PrimitiveType.Sphere, "Torso", model, new Vector3(0, 1.15f, 0), new Vector3(0.62f, 0.55f, 0.55f), robe, false);
        Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0, 1.5f, 0), Vector3.one * 0.42f, skin, false);
        Prim(PrimitiveType.Cylinder, "HatBrim", model, new Vector3(0, 1.66f, 0), new Vector3(0.78f, 0.03f, 0.78f), hat, false);
        MeshObj(cone16, "Hat", model, new Vector3(0, 1.67f, 0), new Vector3(0.55f, 0.85f, 0.55f), hat);
        Prim(PrimitiveType.Cylinder, "HatBand", model, new Vector3(0, 1.73f, 0), new Vector3(0.5f, 0.03f, 0.5f), gold, false);
        Prim(PrimitiveType.Sphere, "EyeL", model, new Vector3(-0.08f, 1.52f, 0.17f), Vector3.one * 0.06f, Mat("Black", Color.black, 0.5f), false);
        Prim(PrimitiveType.Sphere, "EyeR", model, new Vector3(0.08f, 1.52f, 0.17f), Vector3.one * 0.06f, Mat("Black", Color.black, 0.5f), false);
        // посох со светящимся шаром
        Prim(PrimitiveType.Cylinder, "Staff", model, new Vector3(0.5f, 0.85f, 0.2f), new Vector3(0.06f, 0.85f, 0.06f), Mat("Wood", new Color(0.6f, 0.38f, 0.2f), 0.1f), false);
        var orb = Prim(PrimitiveType.Sphere, "Orb", model, new Vector3(0.5f, 1.8f, 0.2f), Vector3.one * 0.26f, orbMat, false);
        var light = new GameObject("OrbLight").AddComponent<Light>(); light.transform.SetParent(orb.transform, false);
        light.type = LightType.Point; light.color = new Color(0.5f, 0.9f, 1f); light.range = 9f; light.intensity = 2.5f;
        // искры вокруг шара
        var psGo = new GameObject("OrbSparks"); psGo.transform.SetParent(orb.transform, false);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 1.0f; main.startSpeed = 0.4f; main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new Color(0.6f, 0.95f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.1f;
        var em = ps.emission; em.rateOverTime = 25;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        var colL = ps.colorOverLifetime; colL.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        colL.color = g;
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = spark;

        spawnPoint = hero.transform;
        return hc;
    }

    static List<Crystal> BuildCrystals(Terrain terrain, Transform root)
    {
        var list = new List<Crystal>();
        var mesh = MakeCone("Crystal6", 6, 0.5f, 1f, true);
        var mat = Mat("Crystal", new Color(0.3f, 0.75f, 1f), 0.95f, new Color(0.3f, 1.1f, 2.6f));
        var holder = new GameObject("Crystals").transform; holder.SetParent(root, false);
        for (int i = 0; i < CrystalSpots.Length; i++)
        {
            var c = new GameObject($"Crystal_{i + 1}").transform; c.SetParent(holder, false);
            c.position = OnGround(terrain, CrystalSpots[i], 0f);
            var vis = MeshObj(mesh, "Visual", c, new Vector3(0, 1.4f, 0), new Vector3(0.9f, 1.8f, 0.9f), mat);
            // маленькие осколки у основания
            MeshObj(mesh, "Shard", c, new Vector3(0.5f, 0.35f, 0.2f), new Vector3(0.35f, 0.7f, 0.35f), mat).transform.rotation = Quaternion.Euler(0, 20, 15);
            MeshObj(mesh, "Shard", c, new Vector3(-0.45f, 0.3f, -0.25f), new Vector3(0.3f, 0.6f, 0.3f), mat).transform.rotation = Quaternion.Euler(0, -35, -18);
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(c, false); l.transform.localPosition = new Vector3(0, 1.6f, 0);
            l.type = LightType.Point; l.color = new Color(0.4f, 0.8f, 1f); l.range = 8f; l.intensity = 2.2f;
            var crystal = c.gameObject.AddComponent<Crystal>(); crystal.visual = vis.transform; crystal.glow = l;
            // световой столб — виден издалека
            var beam = Prim(PrimitiveType.Cylinder, "Beam", c, new Vector3(0, 10f, 0), new Vector3(0.35f, 10f, 0.35f), Mat("Beam", new Color(0.4f, 0.8f, 1f, 1f), 0f, new Color(0.15f, 0.45f, 0.9f)), false);
            beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            list.Add(crystal);
        }
        return list;
    }

    static void SetupLookAndFeel(Transform root)
    {
        // солнце
        var sun = Object.FindFirstObjectByType<Light>();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) sun = l;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0);
            sun.color = new Color(1f, 0.93f, 0.8f); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.72f, 0.85f, 0.95f); RenderSettings.fogDensity = 0.0055f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.25f);

        // постобработка: свечение, цвет, виньетка
        Directory.CreateDirectory("Assets/_Project/Art/Settings");
        string vp = "Assets/_Project/Art/Settings/ForestVolume.asset";
        AssetDatabase.DeleteAsset(vp);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(1.1f); bloom.threshold.Override(0.9f); bloom.scatter.Override(0.7f);
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(22f); ca.contrast.Override(10f); ca.postExposure.Override(0.2f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.25f);
        AssetDatabase.CreateAsset(profile, vp);
        foreach (var o in profile.components) AssetDatabase.AddObjectToAsset(o, profile);
        var vgo = new GameObject("PostProcessing"); vgo.transform.SetParent(root, false);
        var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile;
    }

    // ───────────────────────────── вход ─────────────────────────────
    [MenuItem("Magic World/Собрать первую заготовку")]
    public static void Build()
    {
        Random.InitState(5);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // чистим всё, что строили раньше
        foreach (var n in new[] { "World", "Hero", "Round" })
        { var old = GameObject.Find(n); if (old != null) Object.DestroyImmediate(old); }

        var terrain = Object.FindFirstObjectByType<Terrain>();
        SculptTerrain(terrain);
        PaintTerrain(terrain);
        var tc = terrain.GetComponent<TerrainCollider>(); tc.terrainData = terrain.terrainData;
        terrain.drawInstanced = true;

        var world = new GameObject("World").transform;
        var spark = Mat("Spark", Color.white, 0f); // заменяем шейдер на частичный
        spark.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        spark.SetColor("_BaseColor", Color.white);
        var sparkTex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (sparkTex != null) spark.SetTexture("_BaseMap", sparkTex);
        spark.SetFloat("_Surface", 1); spark.SetFloat("_Blend", 2); // Additive
        spark.SetFloat("_SrcBlend", (float)BlendMode.One); spark.SetFloat("_DstBlend", (float)BlendMode.One); spark.SetFloat("_ZWrite", 0);
        spark.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); spark.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(spark);

        BuildScenery(terrain, world);
        BuildWater(world);
        var far = GameObject.CreatePrimitive(PrimitiveType.Plane); far.name = "FarGround";
        far.transform.SetParent(world, false); far.transform.position = new Vector3(0, 2.6f, 0); far.transform.localScale = new Vector3(150, 1, 150);
        Object.DestroyImmediate(far.GetComponent<Collider>());
        far.GetComponent<Renderer>().sharedMaterial = Mat("FarGround", new Color(0.2f, 0.5f, 0.17f), 0f);
        var crystals = BuildCrystals(terrain, world);
        var hero = BuildHero(terrain, spark, out _);
        SetupLookAndFeel(world);

        // камера
        var cam = Camera.main;
        if (cam == null) { var cgo = new GameObject("Main Camera"); cgo.tag = "MainCamera"; cam = cgo.AddComponent<Camera>(); cgo.AddComponent<AudioListener>(); }
        var tpc = cam.GetComponent<ThirdPersonCamera>() ?? cam.gameObject.AddComponent<ThirdPersonCamera>();
        tpc.target = hero.transform; tpc.yaw = 25f; tpc.pitch = 16f;
        cam.farClipPlane = 600f; cam.clearFlags = CameraClearFlags.Skybox;
        var ud = cam.GetUniversalAdditionalCameraData(); ud.renderPostProcessing = true; ud.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        hero.cameraYaw = cam.transform;
        cam.transform.position = hero.transform.position + new Vector3(-3f, 4f, -6f);

        // раунд
        var round = new GameObject("Round").AddComponent<GameRound>();
        round.hero = hero; round.crystals = crystals; round.sparkMaterial = spark; round.timeLimit = 150f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Magic World] Первая заготовка собрана: " + ScenePath);
    }
}
#endif
