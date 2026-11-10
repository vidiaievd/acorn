using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MagicWorld
{
    /// <summary>Один раунд: найди все синие кристаллы, пока не вышло время.</summary>
    public class GameRound : MonoBehaviour
    {
        public HeroController hero;
        public List<Crystal> crystals = new List<Crystal>();
        public float timeLimit = 150f;
        public float pickupRadius = 1.8f;
        public Material sparkMaterial;

        enum State { Intro, Playing, Won, Lost }
        State state = State.Intro;
        float timeLeft, stateTime;
        int collected, total;
        GUIStyle big, mid, small;
        float flash;

        // TerrainCollider в этом проекте не даёт контактов (лучи видят, предметы проваливаются),
        // поэтому строим физическую землю обычным MeshCollider по рельефу с шагом 1 м.
        void BuildGroundCollider()
        {
            var t = Terrain.activeTerrain;
            if (t == null) return;
            const int n = 201; // 200 м → 201 вершина
            var origin = t.transform.position;
            var size = t.terrainData.size;
            var verts = new Vector3[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = origin.x + size.x * x / (n - 1), wz = origin.z + size.z * z / (n - 1);
                    verts[z * n + x] = new Vector3(wx, origin.y + t.SampleHeight(new Vector3(wx, 0, wz)), wz);
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int k = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris[k++] = i; tris[k++] = i + n; tris[k++] = i + 1;
                    tris[k++] = i + 1; tris[k++] = i + n; tris[k++] = i + n + 1;
                }
            var mesh = new Mesh { name = "GroundPhysics", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.triangles = tris;
            var go = new GameObject("GroundPhysics");
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Physics.SyncTransforms();
        }

        System.Collections.IEnumerator PhysicsProbe()
        {
            yield return new WaitForSeconds(3f);
            var t = Terrain.activeTerrain;
            var p = hero.transform.position;
            var sb = new System.Text.StringBuilder("[Probe3] ");
            sb.AppendLine($"герой y={p.y:F3}, высота земли по Terrain={t.SampleHeight(p) + t.transform.position.y:F3}");
            var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 20f);
            System.Array.Sort(hits, (x, y) => y.point.y.CompareTo(x.point.y));
            foreach (var h in hits) sb.AppendLine($"  луч вниз: {h.collider.name} ({h.collider.GetType().Name}) y={h.point.y:F3}");
            var capsule = hero.GetComponent<CapsuleCollider>();
            sb.AppendLine($"капсула: bounds.min.y={capsule.bounds.min.y:F3}, center={capsule.center}, height={capsule.height}, radius={capsule.radius}");
            Debug.Log(sb.ToString());
        }

        void Awake() { BuildGroundCollider(); }

        void Start()
        {
            StartCoroutine(PhysicsProbe());
            total = crystals.Count;
            timeLeft = timeLimit;
            hero.InputEnabled = true;
        }

        void Update()
        {
            stateTime += Time.deltaTime;
            flash = Mathf.Max(0, flash - Time.deltaTime * 2f);

            switch (state)
            {
                case State.Intro:
                    if (stateTime > 4f) { state = State.Playing; stateTime = 0; }
                    goto case State.Playing;
                case State.Playing:
                    if (state == State.Playing) timeLeft -= Time.deltaTime;
                    for (int i = crystals.Count - 1; i >= 0; i--)
                    {
                        var c = crystals[i];
                        if (c == null) { crystals.RemoveAt(i); continue; }
                        if ((c.transform.position + Vector3.up * 0.5f - hero.transform.position - Vector3.up * 1f).sqrMagnitude < pickupRadius * pickupRadius)
                        {
                            Collect(c); crystals.RemoveAt(i); collected++; flash = 1f;
                        }
                    }
                    if (collected >= total) { state = State.Won; stateTime = 0; Burst(hero.transform.position + Vector3.up, 120); }
                    else if (timeLeft <= 0) { state = State.Lost; stateTime = 0; timeLeft = 0; }
                    break;
                case State.Won:
                case State.Lost:
                    if (stateTime > 1f && RestartPressed()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    if (state == State.Won && Random.value < 0.08f)
                        Burst(hero.transform.position + new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 5f), Random.Range(-3f, 3f)), 30);
                    break;
            }
        }

        static bool RestartPressed()
        {
            var kb = Keyboard.current; var gp = Gamepad.current;
            return (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        }

        void Collect(Crystal c)
        {
            Burst(c.transform.position + Vector3.up * 0.8f, 60);
            Destroy(c.gameObject);
        }

        void Burst(Vector3 pos, int count)
        {
            var go = new GameObject("Sparkles");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.duration = 1f; main.loop = false;
            main.startLifetime = 1.1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.9f, 0.4f));
            main.gravityModifier = 0.5f;
            var em = ps.emission; em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (sparkMaterial != null) r.sharedMaterial = sparkMaterial;
            ps.Play();
            Destroy(go, 2.5f);
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                mid = new GUIStyle(big);
                small = new GUIStyle(big) { alignment = TextAnchor.UpperLeft };
            }
            float s = Screen.height / 1080f;
            big.fontSize = Mathf.RoundToInt(90 * s);
            mid.fontSize = Mathf.RoundToInt(38 * s);
            small.fontSize = Mathf.RoundToInt(34 * s);

            // счётчик кристаллов
            DrawText(new Rect(30 * s, 20 * s, 700 * s, 60 * s), $"Кристаллы: {collected} / {total}", small, new Color(0.55f, 0.9f, 1f));
            // таймер
            int t = Mathf.CeilToInt(timeLeft);
            var timeColor = timeLeft < 20 && state == State.Playing ? new Color(1f, 0.45f, 0.4f) : Color.white;
            DrawText(new Rect(Screen.width - 330 * s, 20 * s, 300 * s, 60 * s), $"Время: {t / 60}:{t % 60:00}", small, timeColor);

            // подсказка: расстояние до ближайшего кристалла
            if (state == State.Playing && crystals.Count > 0)
            {
                float best = float.MaxValue;
                foreach (var c in crystals) if (c != null) best = Mathf.Min(best, Vector3.Distance(c.transform.position, hero.transform.position));
                DrawText(new Rect(30 * s, 70 * s, 800 * s, 50 * s), $"Ближайший кристалл: {Mathf.RoundToInt(best)} м", small, new Color(1, 1, 1, 0.8f));
            }

            var full = new Rect(0, 0, Screen.width, Screen.height);
            if (state == State.Intro)
                DrawText(new Rect(0, Screen.height * 0.18f, Screen.width, 120 * s), $"Найди {total} синих кристаллов!", mid, Color.white);
            if (state == State.Playing || state == State.Intro)
                DrawText(new Rect(0, Screen.height - 70 * s, Screen.width, 50 * s), "WASD — бежать · Пробел — прыжок · Shift — быстрее · Мышь — камера", mid, new Color(1, 1, 1, 0.55f), 0.6f);
            if (state == State.Won)
            {
                DrawText(new Rect(0, Screen.height * 0.28f, Screen.width, 140 * s), "ПОБЕДА!", big, new Color(1f, 0.9f, 0.3f));
                DrawText(new Rect(0, Screen.height * 0.28f + 130 * s, Screen.width, 60 * s), $"Все кристаллы собраны! Осталось {Mathf.CeilToInt(timeLeft)} сек. · R — ещё раз", mid, Color.white);
            }
            if (state == State.Lost)
            {
                DrawText(new Rect(0, Screen.height * 0.28f, Screen.width, 140 * s), "Время вышло!", big, new Color(1f, 0.5f, 0.45f));
                DrawText(new Rect(0, Screen.height * 0.28f + 130 * s, Screen.width, 60 * s), $"Собрано {collected} из {total} · R — попробовать снова", mid, Color.white);
            }
            if (flash > 0)
            {
                var old = GUI.color; GUI.color = new Color(0.5f, 0.9f, 1f, flash * 0.25f);
                GUI.DrawTexture(full, Texture2D.whiteTexture); GUI.color = old;
            }
        }

        static void DrawText(Rect r, string text, GUIStyle style, Color color, float scale = 1f)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f * color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }
    }
}
