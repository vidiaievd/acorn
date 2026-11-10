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

        void Awake()
        {
            // физическая земля: проверяем и при необходимости создаём коллайдер Terrain заново
            var terrain = Terrain.activeTerrain;
            var origin = hero.transform.position + Vector3.up * 100f;
            bool ok = Physics.Raycast(origin, Vector3.down, out var hit, 300f);
            Debug.Log($"[Round] до пересборки: terrain={(terrain != null)}, коллайдер={(terrain != null && terrain.GetComponent<TerrainCollider>() != null)}, луч попал={ok}, y={(ok ? hit.point.y : -999)}, коллайдер='{(ok ? hit.collider.name : "-")}', hero.y={hero.transform.position.y}");
            if (terrain != null)
            {
                var old = terrain.GetComponent<TerrainCollider>();
                var data = terrain.terrainData;
                if (old != null) DestroyImmediate(old);
                var fresh = terrain.gameObject.AddComponent<TerrainCollider>();
                fresh.terrainData = data;
                Physics.SyncTransforms();
                ok = Physics.Raycast(origin, Vector3.down, out hit, 300f);
                Debug.Log($"[Round] после пересборки: луч попал={ok}, y={(ok ? hit.point.y : -999)}, размер данных={data.size}, sample={terrain.SampleHeight(hero.transform.position)}");
            }
        }

        void Start()
        {
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
