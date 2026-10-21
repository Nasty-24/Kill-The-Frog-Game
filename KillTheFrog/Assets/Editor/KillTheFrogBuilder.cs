using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Generates every asset the game needs (sprites, sounds, prefabs) and builds the
// game scene. Run from the menu "Kill The Frog > Build Game Scene", or headless:
//   Unity.exe -batchmode -quit -projectPath <path> -executeMethod KillTheFrogBuilder.Build
public static class KillTheFrogBuilder
{
    const string ArtDir = "Assets/Art";
    const string AudioDir = "Assets/Audio";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/KillTheFrog.unity";
    const int SampleRate = 44100;

    static Font font;
    static Sprite uiSprite;

    [MenuItem("Kill The Frog/Build Game Scene")]
    public static void Build()
    {
        foreach (var dir in new[] { ArtDir, AudioDir, PrefabDir, "Assets/Scenes" })
            Directory.CreateDirectory(dir);

        // --- Sprites -------------------------------------------------------
        var frogSprite = SaveSprite(MakeFrogTexture(false), ArtDir + "/Frog.png", 200);
        var deadSprite = SaveSprite(MakeFrogTexture(true), ArtDir + "/FrogDead.png", 200);
        var bgSprite = SaveSprite(MakeBackgroundTexture(), ArtDir + "/Pond.png", 54);
        var dotSprite = SaveSprite(MakeDotTexture(), ArtDir + "/Dot.png", 64);

        // --- Sounds --------------------------------------------------------
        var startClip = SaveClip(AudioDir + "/Start.wav", Tones(new[] { 523f, 784f }, 0.11f));
        var hitClip = SaveClip(AudioDir + "/Splat.wav", Splat());
        var missClip = SaveClip(AudioDir + "/Miss.wav", Buzz());
        var escapeClip = SaveClip(AudioDir + "/Ribbit.wav", Ribbit());
        var levelClip = SaveClip(AudioDir + "/LevelUp.wav", Tones(new[] { 523f, 659f, 784f, 1047f }, 0.1f));
        var overClip = SaveClip(AudioDir + "/GameOver.wav", Tones(new[] { 392f, 330f, 262f, 196f }, 0.18f));

        // --- Prefabs -------------------------------------------------------
        var splat = BuildSplatPrefab(dotSprite);
        var frog = BuildFrogPrefab(frogSprite, deadSprite, splat);

        // --- Scene ---------------------------------------------------------
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.45f, 0.25f);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        var bg = new GameObject("Background");
        var bgRenderer = bg.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = bgSprite;
        bgRenderer.sortingOrder = -100;
        bg.AddComponent<BackgroundFit>();

        var spawnerGo = new GameObject("FrogSpawner");
        var spawner = spawnerGo.AddComponent<FrogSpawner>();
        spawner.frogPrefab = frog;

        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gm.spawner = spawner;
        gm.sfx = gmGo.AddComponent<AudioSource>();
        gm.sfx.playOnAwake = false;
        gm.startClip = startClip;
        gm.hitClip = hitClip;
        gm.missClip = missClip;
        gm.escapeClip = escapeClip;
        gm.levelUpClip = levelClip;
        gm.gameOverClip = overClip;

        BuildUI(gm);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Kill the Frog";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        AssetDatabase.SaveAssets();
        Debug.Log("[KillTheFrog] Scene built at " + ScenePath);
    }

    // ===================================================================== UI

    static void BuildUI(GameManager gm)
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        gm.canvas = canvas;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // HUD bar across the top.
        var hud = Panel(canvas.transform, "HUD", new Color(0f, 0f, 0f, 0.45f));
        var hudRt = (RectTransform)hud.transform;
        hudRt.anchorMin = new Vector2(0f, 1f);
        hudRt.anchorMax = new Vector2(1f, 1f);
        hudRt.pivot = new Vector2(0.5f, 1f);
        hudRt.sizeDelta = new Vector2(0f, 100f);
        hudRt.anchoredPosition = Vector2.zero;
        gm.hud = hud;
        gm.scoreText = HudText(hud.transform, "Score", "Score: 0", 0.02f, TextAnchor.MiddleLeft);
        gm.levelText = HudText(hud.transform, "Level", "Level 1", 0.30f, TextAnchor.MiddleCenter);
        gm.timerText = HudText(hud.transform, "Timer", "Time: 60", 0.62f, TextAnchor.MiddleCenter);
        gm.livesText = HudText(hud.transform, "Lives", "Lives: 3", 0.80f, TextAnchor.MiddleRight);

        // Level banner in the middle of the screen.
        gm.bannerText = Label(canvas.transform, "Banner", "Level 1", 130, new Color(1f, 0.92f, 0.3f),
            Vector2.zero, new Vector2(1600f, 220f));
        gm.bannerText.raycastTarget = false;

        // Start menu.
        var start = Panel(canvas.transform, "StartPanel", new Color(0.03f, 0.12f, 0.05f, 0.82f));
        Stretch(start);
        Label(start.transform, "Title", "KILL THE FROG", 150, new Color(0.55f, 1f, 0.45f),
            new Vector2(0f, 290f), new Vector2(1800f, 200f));
        Label(start.transform, "Instructions",
            "Frogs pop up around the pond. Click (or tap) them before they hop away!\n" +
            "+1 for every frog   |   -1 for a wrong click   |   lose a life when a frog escapes\n" +
            "Reach 10 points for Level 2 and 25 for Level 3. You have 3 lives and 60 seconds.",
            40, Color.white, new Vector2(0f, 70f), new Vector2(1700f, 220f));
        gm.menuHighScoreText = Label(start.transform, "HighScore", "High score: 0", 42,
            new Color(1f, 0.92f, 0.3f), new Vector2(0f, -90f), new Vector2(900f, 70f));
        Button(start.transform, "StartButton", "START", new Vector2(0f, -230f), gm.StartGame);
        gm.startPanel = start;

        // Game over screen.
        var over = Panel(canvas.transform, "GameOverPanel", new Color(0.12f, 0.02f, 0.02f, 0.85f));
        Stretch(over);
        Label(over.transform, "Title", "GAME OVER", 140, new Color(1f, 0.4f, 0.35f),
            new Vector2(0f, 300f), new Vector2(1600f, 180f));
        gm.gameOverReasonText = Label(over.transform, "Reason", "Time's up!", 50, Color.white,
            new Vector2(0f, 180f), new Vector2(1600f, 80f));
        gm.finalScoreText = Label(over.transform, "FinalScore", "Score: 0", 44, new Color(1f, 0.92f, 0.3f),
            new Vector2(0f, 10f), new Vector2(1600f, 260f));
        Button(over.transform, "RestartButton", "RESTART", new Vector2(-190f, -220f), gm.StartGame);
        Button(over.transform, "MenuButton", "MENU", new Vector2(190f, -220f), gm.ShowMenu);
        gm.gameOverPanel = over;
    }

    static GameObject Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Text Label(Transform parent, string name, string text, int size, Color color, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    static Text HudText(Transform parent, string name, string text, float anchorX, TextAnchor align)
    {
        var t = Label(parent, name, text, 50, Color.white, Vector2.zero, Vector2.zero);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(anchorX, 0f);
        rt.anchorMax = new Vector2(anchorX + 0.18f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        t.alignment = align;
        return t;
    }

    static void Button(Transform parent, string name, string label, Vector2 pos, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(340f, 110f);
        var img = go.AddComponent<Image>();
        img.sprite = uiSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.3f, 0.75f, 0.3f);
        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 1f, 0.85f);
        colors.pressedColor = new Color(0.6f, 0.8f, 0.6f);
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        var text = Label(go.transform, "Label", label, 52, Color.white, Vector2.zero, Vector2.zero);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    // ================================================================ Prefabs

    static Frog BuildFrogPrefab(Sprite alive, Sprite dead, ParticleSystem splat)
    {
        var go = new GameObject("Frog");
        go.AddComponent<SpriteRenderer>().sprite = alive;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        go.AddComponent<CircleCollider2D>().radius = 0.55f;
        var frog = go.AddComponent<Frog>();
        frog.deadSprite = dead;
        frog.splatPrefab = splat;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Frog.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab.GetComponent<Frog>();
    }

    static ParticleSystem BuildSplatPrefab(Sprite dot)
    {
        var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dot.texture };
        AssetDatabase.CreateAsset(mat, ArtDir + "/Splat.mat");

        var go = new GameObject("SplatEffect");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.85f, 0.2f), new Color(0.9f, 0.2f, 0.15f));
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.sortingOrder = 50;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/SplatEffect.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab.GetComponent<ParticleSystem>();
    }

    // =============================================================== Textures

    static Sprite SaveSprite(Texture2D tex, string path, float pixelsPerUnit)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Small signed-distance painter: each shape is blended over the pixel with
    // a one-pixel antialiased edge.
    class Painter
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Painter(int w, int h, Color fill)
        {
            W = w; H = h;
            Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public void Shape(Func<float, float, float> sdf, Color c)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d) * c.a;
                if (a <= 0f) continue;
                int i = y * W + x;
                Color dst = Px[i];
                float outA = a + dst.a * (1f - a);
                Color rgb = (new Color(c.r, c.g, c.b) * a + new Color(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / Mathf.Max(outA, 1e-5f);
                Px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
            }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            Shape((x, y) =>
            {
                float nx = (x - cx) / rx, ny = (y - cy) / ry;
                return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
            }, c);
        }

        public void Line(float ax, float ay, float bx, float by, float width, Color c)
        {
            Shape((x, y) =>
            {
                Vector2 p = new Vector2(x - ax, y - ay), ab = new Vector2(bx - ax, by - ay);
                float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
                return (p - ab * t).magnitude - width * 0.5f;
            }, c);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(Px);
            tex.Apply();
            return tex;
        }
    }

    static Texture2D MakeFrogTexture(bool dead)
    {
        var p = new Painter(256, 256, Color.clear);
        Color outline = dead ? new Color(0.18f, 0.25f, 0.15f) : new Color(0.08f, 0.3f, 0.08f);
        Color body = dead ? new Color(0.45f, 0.55f, 0.35f) : new Color(0.3f, 0.78f, 0.25f);
        Color belly = dead ? new Color(0.7f, 0.72f, 0.55f) : new Color(0.75f, 0.92f, 0.45f);

        // Feet
        foreach (float s in new[] { -1f, 1f })
        {
            p.Ellipse(128 + s * 78, 42, 34, 16, outline);
            p.Ellipse(128 + s * 78, 44, 29, 11, body);
        }
        // Body with outline
        p.Ellipse(128, 100, 100, 74, outline);
        p.Ellipse(128, 100, 93, 67, body);
        p.Ellipse(128, 82, 58, 38, belly);
        // Eye bumps
        foreach (float s in new[] { -1f, 1f })
        {
            float ex = 128 + s * 52;
            p.Ellipse(ex, 170, 38, 38, outline);
            p.Ellipse(ex, 170, 32, 32, body);
            p.Ellipse(ex, 172, 23, 23, Color.white);
            if (dead)
            {
                p.Line(ex - 12, 160, ex + 12, 184, 7, Color.black);
                p.Line(ex - 12, 184, ex + 12, 160, 7, Color.black);
            }
            else
            {
                p.Ellipse(ex + s * 3, 170, 11, 14, Color.black);
                p.Ellipse(ex + s * 3 - 4, 176, 4, 4, Color.white);
            }
        }
        // Cheeks + mouth
        if (!dead)
        {
            p.Ellipse(70, 112, 11, 7, new Color(1f, 0.5f, 0.5f, 0.6f));
            p.Ellipse(186, 112, 11, 7, new Color(1f, 0.5f, 0.5f, 0.6f));
            for (int i = 0; i < 12; i++)
            {
                float a0 = Mathf.Lerp(-2.4f, -0.74f, i / 12f), a1 = Mathf.Lerp(-2.4f, -0.74f, (i + 1) / 12f);
                p.Line(128 + Mathf.Cos(a0) * 40, 132 + Mathf.Sin(a0) * 22,
                       128 + Mathf.Cos(a1) * 40, 132 + Mathf.Sin(a1) * 22, 6, outline);
            }
        }
        else
        {
            p.Line(98, 116, 158, 116, 6, outline);
            p.Ellipse(140, 108, 9, 12, new Color(0.9f, 0.35f, 0.4f)); // tongue
        }
        return p.ToTexture();
    }

    static Texture2D MakeBackgroundTexture()
    {
        const int w = 960, h = 540;
        var p = new Painter(w, h, Color.white);
        // Grass with Perlin variation
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.6f + Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.4f;
            p.Px[y * w + x] = Color.Lerp(new Color(0.22f, 0.5f, 0.2f), new Color(0.35f, 0.65f, 0.28f), n);
        }
        // Pond
        p.Ellipse(480, 250, 420, 210, new Color(0.45f, 0.35f, 0.2f));
        p.Ellipse(480, 252, 405, 197, new Color(0.25f, 0.55f, 0.75f));
        p.Ellipse(470, 260, 330, 150, new Color(0.3f, 0.62f, 0.82f));
        // Ripples
        foreach (var r in new[] { new Vector3(300, 300, 40), new Vector3(640, 200, 55), new Vector3(520, 330, 28) })
        {
            p.Shape((x, y) => Mathf.Abs(new Vector2(x - r.x, (y - r.y) * 1.8f).magnitude - r.z) - 1.5f,
                new Color(1f, 1f, 1f, 0.35f));
        }
        // Lily pads
        foreach (var l in new[] { new Vector3(210, 220, 36), new Vector3(760, 300, 42), new Vector3(580, 120, 30), new Vector3(380, 170, 26) })
        {
            p.Ellipse(l.x, l.y, l.z, l.z * 0.7f, new Color(0.15f, 0.45f, 0.15f));
            p.Ellipse(l.x, l.y, l.z * 0.9f, l.z * 0.62f, new Color(0.28f, 0.68f, 0.25f));
            var lc = l;
            p.Shape((x, y) =>
            {
                // notch cut out of the pad
                Vector2 d = new Vector2(x - lc.x, y - lc.y);
                return (Mathf.Abs(Mathf.Atan2(d.y, d.x) - 0.3f) < 0.25f && d.magnitude < lc.z) ? -1f : 1f;
            }, new Color(0.3f, 0.62f, 0.82f));
        }
        // Reeds around the edges
        var rng = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            float x = (float)rng.NextDouble() * w;
            float y = rng.NextDouble() < 0.5 ? (float)rng.NextDouble() * 50 : h - (float)rng.NextDouble() * 60;
            float len = 30 + (float)rng.NextDouble() * 40;
            p.Line(x, y, x + ((float)rng.NextDouble() - 0.5f) * 14, y + len, 4, new Color(0.15f, 0.38f, 0.12f));
        }
        return p.ToTexture();
    }

    static Texture2D MakeDotTexture()
    {
        var p = new Painter(64, 64, Color.clear);
        p.Ellipse(32, 32, 28, 28, Color.white);
        return p.ToTexture();
    }

    // ================================================================= Audio

    static AudioClip SaveClip(string path, float[] samples)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataBytes);
            bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);          // PCM
            bw.Write((short)1);          // mono
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);    // byte rate
            bw.Write((short)2);          // block align
            bw.Write((short)16);         // bits per sample
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataBytes);
            foreach (float s in samples)
                bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static float[] Splat()
    {
        var rng = new System.Random(1);
        int n = (int)(SampleRate * 0.3f);
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float freq = Mathf.Lerp(320f, 60f, t / 0.3f);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 30f);
            s[i] = (Mathf.Sin(phase) * 0.7f * Mathf.Exp(-t * 12f) + noise * 0.6f) * 0.8f;
        }
        return s;
    }

    static float[] Buzz()
    {
        int n = (int)(SampleRate * 0.16f);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 130f * t));
            s[i] = sq * 0.3f * (1f - t / 0.16f);
        }
        return s;
    }

    static float[] Ribbit()
    {
        int n = (int)(SampleRate * 0.4f);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            // Two croaks: sawtooth with fast amplitude wobble.
            float gate = (t < 0.15f || (t > 0.2f && t < 0.36f)) ? 1f : 0f;
            float freq = t < 0.18f ? 190f : 160f;
            float saw = 2f * Mathf.Repeat(freq * t, 1f) - 1f;
            float wobble = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 35f * t);
            s[i] = saw * wobble * gate * 0.45f;
        }
        return s;
    }

    static float[] Tones(float[] freqs, float noteLength)
    {
        int per = (int)(SampleRate * noteLength);
        var s = new float[per * freqs.Length];
        for (int k = 0; k < freqs.Length; k++)
        for (int i = 0; i < per; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 6f);
            s[k * per + i] = (Mathf.Sin(2f * Mathf.PI * freqs[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[k] * t)) * 0.35f * env;
        }
        return s;
    }
}
