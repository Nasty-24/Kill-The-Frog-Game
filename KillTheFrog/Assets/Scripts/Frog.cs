using System.Collections;
using UnityEngine;

// A single frog. Moves by hopping with Rigidbody2D impulses (Unity 2D physics),
// squashes and stretches with its speed, blinks before escaping, and plays a
// splat effect when killed.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class Frog : MonoBehaviour
{
    public int points = 1;
    public float hopInterval = 0.7f;
    public Sprite deadSprite;
    public ParticleSystem splatPrefab;

    public bool IsAlive { get; private set; }

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    Vector3 baseScale;
    float popScale = 0f;
    float lifetime;
    float hopForce;
    float age;
    float nextHop;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    public void Init(float life, float force)
    {
        lifetime = life;
        hopForce = force;
        age = 0f;
        nextHop = Random.Range(0.15f, hopInterval);
        IsAlive = true;
        StartCoroutine(PopIn());
    }

    void Update()
    {
        if (!IsAlive) return;

        age += Time.deltaTime;
        float remaining = lifetime - age;

        // Blink during the last moments to warn the player.
        sr.enabled = remaining > 0.8f || Mathf.Repeat(remaining * 8f, 1f) > 0.35f;

        if (remaining <= 0f)
        {
            Escape();
            return;
        }

        if (age >= nextHop) Hop();

        // Squash & stretch driven by the physics velocity.
        Vector2 v = rb.linearVelocity;
        float stretch = Mathf.Clamp(v.magnitude * 0.07f, 0f, 0.3f);
        transform.localScale = new Vector3(
            baseScale.x * (1f - stretch * 0.5f),
            baseScale.y * (1f + stretch),
            1f) * popScale;
        if (Mathf.Abs(v.x) > 0.1f) sr.flipX = v.x < 0f;
    }

    void Hop()
    {
        // Random direction, nudged back toward the middle of the screen.
        Vector2 dir = Random.insideUnitCircle.normalized - (Vector2)transform.position * 0.12f;
        rb.AddForce(dir.normalized * hopForce * Random.Range(0.8f, 1.2f), ForceMode2D.Impulse);
        nextHop = age + hopInterval * Random.Range(0.7f, 1.3f);
    }

    public void Kill()
    {
        if (!IsAlive) return;
        IsAlive = false;
        StopAllCoroutines();
        col.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        sr.enabled = true;
        if (deadSprite) sr.sprite = deadSprite;
        if (splatPrefab) Instantiate(splatPrefab, transform.position, Quaternion.identity);
        StartCoroutine(DieAnimation());
    }

    void Escape()
    {
        IsAlive = false;
        col.enabled = false;
        sr.enabled = true;
        if (GameManager.Instance) GameManager.Instance.FrogEscaped(this);
        StartCoroutine(ShrinkAway());
    }

    IEnumerator PopIn()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            // Overshoot slightly for a bouncy entrance.
            popScale = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f * (1f - t);
            popScale *= t;
            yield return null;
        }
        popScale = 1f;
    }

    IEnumerator DieAnimation()
    {
        Vector3 start = baseScale * Mathf.Max(popScale, 0.5f);
        Color c = sr.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            transform.localScale = new Vector3(start.x * (1f + t * 0.4f), start.y * (1f - t * 0.6f), 1f);
            sr.color = new Color(c.r, c.g, c.b, 1f - t);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator ShrinkAway()
    {
        Vector3 start = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            transform.localScale = start * (1f - t);
            yield return null;
        }
        Destroy(gameObject);
    }
}
