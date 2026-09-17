using System.Collections;
using UnityEngine;

// A projectile: moves in a straight line, destroys any fly it touches,
// and bursts (scales up + fades out) when it hits a wall/obstacle instead of
// just vanishing instantly.
public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f;
    public float burstDuration = 0.15f;

    private Vector2 direction;
    private SpriteRenderer spriteRenderer;
    private bool hasBurst = false;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime); // clean up automatically if it never hits anything
    }

    private void Update()
    {
        if (hasBurst)
            return; // don't keep moving once it's bursting

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBurst)
            return;

        FlyFSM fly = other.GetComponent<FlyFSM>();
        if (fly != null)
        {
            Destroy(fly.gameObject);
            Destroy(gameObject);
            return;
        }

        // ignore the player itself, in case of spawn overlap
        if (other.GetComponent<PlayerMovement>() != null)
            return;

        // hit something solid that isn't a fly will burst instead of vanishing
        StartCoroutine(Burst());
    }

    private IEnumerator Burst()
    {
        hasBurst = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false; // stop registering more hits while bursting

        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * 2.5f;
        Color startColor = spriteRenderer.color;

        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;

            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            Color fadedColor = startColor;
            fadedColor.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = fadedColor;

            yield return null;
        }

        Destroy(gameObject);
    }
}