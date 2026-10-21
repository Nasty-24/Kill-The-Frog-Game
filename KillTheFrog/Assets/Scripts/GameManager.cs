using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum GameState { Menu, Playing, GameOver }

// Owns the game rules: input handling, score, lives, timer, levels, UI and sound.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Rules")]
    public int startingLives = 3;
    public float roundTime = 60f;
    public int wrongClickPenalty = 1;
    public LevelSettings[] levels =
    {
        new LevelSettings { name = "Level 1", scoreToReach = 0,  spawnInterval = 1.4f, frogLifetime = 3.0f, hopForce = 2.5f, maxFrogs = 3 },
        new LevelSettings { name = "Level 2", scoreToReach = 10, spawnInterval = 1.0f, frogLifetime = 2.3f, hopForce = 4.0f, maxFrogs = 5 },
        new LevelSettings { name = "Level 3", scoreToReach = 25, spawnInterval = 0.7f, frogLifetime = 1.7f, hopForce = 6.0f, maxFrogs = 7 },
    };

    [Header("Scene")]
    public FrogSpawner spawner;
    public Canvas canvas;

    [Header("UI")]
    public GameObject hud;
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public Text scoreText;
    public Text livesText;
    public Text timerText;
    public Text levelText;
    public Text bannerText;
    public Text gameOverReasonText;
    public Text finalScoreText;
    public Text menuHighScoreText;

    [Header("Audio")]
    public AudioSource sfx;
    public AudioClip startClip;
    public AudioClip hitClip;
    public AudioClip missClip;
    public AudioClip escapeClip;
    public AudioClip levelUpClip;
    public AudioClip gameOverClip;

    public GameState State { get; private set; } = GameState.Menu;

    const string HighScoreKey = "KillTheFrog.HighScore";

    int score;
    int lives;
    int levelIndex;
    float timeLeft;
    int kills;
    int misses;
    Camera cam;
    Coroutine bannerRoutine;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    void Start()
    {
        ShowMenu();
    }

    public void ShowMenu()
    {
        State = GameState.Menu;
        startPanel.SetActive(true);
        gameOverPanel.SetActive(false);
        hud.SetActive(false);
        bannerText.gameObject.SetActive(false);
        menuHighScoreText.text = "High score: " + PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    public void StartGame()
    {
        spawner.Stop();
        spawner.ClearAll();

        score = 0;
        kills = 0;
        misses = 0;
        lives = startingLives;
        timeLeft = roundTime;
        levelIndex = 0;
        spawner.Settings = levels[0];

        startPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        hud.SetActive(true);
        State = GameState.Playing;
        UpdateHud();
        ShowBanner(levels[0].name);
        Play(startClip);
        spawner.Begin();
    }

    void Update()
    {
        if (State != GameState.Playing) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            EndGame("Time's up!");
            return;
        }

        // Left mouse button; touch input is reported as mouse clicks too.
        if (Input.GetMouseButtonDown(0)) HandleClick(Input.mousePosition);

        UpdateHud();
    }

    void HandleClick(Vector2 screenPos)
    {
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 world = cam.ScreenToWorldPoint(screenPos);
        Collider2D hit = Physics2D.OverlapPoint(world);
        Frog frog = hit ? hit.GetComponent<Frog>() : null;

        if (frog && frog.IsAlive)
        {
            frog.Kill();
            kills++;
            score += frog.points;
            Play(hitClip);
            Popup("+" + frog.points, new Color(1f, 0.95f, 0.3f), screenPos);
            CheckLevelUp();
        }
        else
        {
            misses++;
            score = Mathf.Max(0, score - wrongClickPenalty);
            Play(missClip);
            Popup("-" + wrongClickPenalty, new Color(1f, 0.35f, 0.3f), screenPos);
        }
    }

    public void FrogEscaped(Frog frog)
    {
        if (State != GameState.Playing) return;

        lives--;
        Play(escapeClip);
        Popup("Escaped!", new Color(1f, 0.5f, 0.2f), cam.WorldToScreenPoint(frog.transform.position));
        UpdateHud();
        if (lives <= 0) EndGame("A frog got away... out of lives!");
    }

    void CheckLevelUp()
    {
        int next = levelIndex + 1;
        if (next < levels.Length && score >= levels[next].scoreToReach)
        {
            levelIndex = next;
            spawner.Settings = levels[levelIndex];
            Play(levelUpClip);
            ShowBanner(levels[levelIndex].name + "!");
        }
    }

    void EndGame(string reason)
    {
        State = GameState.GameOver;
        spawner.Stop();
        spawner.ClearAll();
        UpdateHud();

        int best = PlayerPrefs.GetInt(HighScoreKey, 0);
        bool newBest = score > best;
        if (newBest)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
            best = score;
        }

        gameOverReasonText.text = reason;
        finalScoreText.text =
            "Score: " + score + (newBest ? "   NEW HIGH SCORE!" : "") +
            "\nReached: " + levels[levelIndex].name +
            "\nFrogs killed: " + kills + "    Wrong clicks: " + misses +
            "\nHigh score: " + best;
        gameOverPanel.SetActive(true);
        Play(gameOverClip);
    }

    void UpdateHud()
    {
        scoreText.text = "Score: " + score;
        livesText.text = "Lives: " + Mathf.Max(lives, 0);
        timerText.text = "Time: " + Mathf.CeilToInt(timeLeft);
        timerText.color = timeLeft <= 10f ? new Color(1f, 0.4f, 0.35f) : Color.white;
        levelText.text = levels[levelIndex].name;
    }

    void Play(AudioClip clip)
    {
        if (clip && sfx) sfx.PlayOneShot(clip);
    }

    void ShowBanner(string message)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(message));
    }

    IEnumerator BannerRoutine(string message)
    {
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
        Color c = bannerText.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            float scale = 1f + Mathf.Max(0f, 0.3f - t) * 1.5f;
            bannerText.transform.localScale = Vector3.one * scale;
            bannerText.color = new Color(c.r, c.g, c.b, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
            yield return null;
        }
        bannerText.color = c;
        bannerText.gameObject.SetActive(false);
    }

    // Floating "+1" / "-1" text that drifts up and fades out.
    void Popup(string message, Color color, Vector2 screenPos)
    {
        var go = new GameObject("Popup", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var text = go.AddComponent<Text>();
        text.font = scoreText.font;
        text.fontSize = 46;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = message;
        text.color = color;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        go.transform.position = screenPos;
        StartCoroutine(PopupRoutine(text));
    }

    IEnumerator PopupRoutine(Text text)
    {
        Vector3 start = text.transform.position;
        Color c = text.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            text.transform.position = start + Vector3.up * (80f * t);
            text.color = new Color(c.r, c.g, c.b, 1f - t * t);
            yield return null;
        }
        Destroy(text.gameObject);
    }
}
