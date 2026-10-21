using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns frogs at random positions inside the camera view at timed intervals,
// and walls the view with an edge collider so hopping frogs stay on screen.
public class FrogSpawner : MonoBehaviour
{
    public Frog frogPrefab;
    public float sidePadding = 0.9f;
    public float topPadding = 1.6f;      // keep clear of the HUD bar
    public float bottomPadding = 0.9f;

    public LevelSettings Settings { get; set; }

    readonly List<Frog> active = new List<Frog>();
    Coroutine loop;
    Camera cam;

    void Awake()
    {
        cam = Camera.main;
        BuildWalls();
    }

    void BuildWalls()
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        var edge = gameObject.AddComponent<EdgeCollider2D>();
        edge.points = new[]
        {
            new Vector2(-w, -h), new Vector2(-w, h - 1f),
            new Vector2(w, h - 1f), new Vector2(w, -h), new Vector2(-w, -h)
        };
    }

    public void Begin()
    {
        Stop();
        loop = StartCoroutine(SpawnLoop());
    }

    public void Stop()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    public void ClearAll()
    {
        foreach (var f in active)
            if (f) Destroy(f.gameObject);
        active.Clear();
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(0.5f);
        while (true)
        {
            active.RemoveAll(f => f == null || !f.IsAlive);
            if (active.Count < Settings.maxFrogs) Spawn();
            yield return new WaitForSeconds(Settings.spawnInterval * Random.Range(0.8f, 1.2f));
        }
    }

    void Spawn()
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        var pos = new Vector3(
            Random.Range(-w + sidePadding, w - sidePadding),
            Random.Range(-h + bottomPadding, h - topPadding),
            0f);
        var frog = Instantiate(frogPrefab, pos, Quaternion.identity);
        frog.GetComponent<SpriteRenderer>().sortingOrder = active.Count;
        frog.Init(Settings.frogLifetime, Settings.hopForce);
        active.Add(frog);
    }
}
