using UnityEngine;

// Scales the background sprite so it always covers the camera view.
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFit : MonoBehaviour
{
    void Start()
    {
        var cam = Camera.main;
        var size = GetComponent<SpriteRenderer>().sprite.bounds.size;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        float s = Mathf.Max(w / size.x, h / size.y);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
