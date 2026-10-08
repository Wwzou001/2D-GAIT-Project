using System.Collections;
using UnityEngine;
 
// A flame that flies in a straight line. It is created at runtime by PlayerShooting.
//
// Hits a fly:        the fly is collected straight away. It is removed and
//                    GameManager is told, so the counter goes up.
// Hits a wall or an obstacle: it bursts (grows and fades) instead of just vanishing.
// Passes through:    the player and other projectiles.
public class Projectile : MonoBehaviour
{
    public float speed = 5f;
    public float lifetime = 3f;          // removed automatically if it never hits anything
    public float burstDuration = 0.15f;  // how long the burst effect lasts
 
    private Vector2 direction;
    private SpriteRenderer spriteRenderer;
 
    // Becomes true after the projectile has hit something, so it can only hit once.
    private bool isFinished = false;
 
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
        Destroy(gameObject, lifetime);
    }
 
    private void Update()
    {
        if (isFinished)
            return;
 
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }
 
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isFinished)
            return;
 
        // Things the projectile should fly straight through.
        if (other.GetComponent<PlayerMovement>() != null) return;
        if (other.GetComponent<Projectile>() != null) return;
 
        // Hit a fly: it counts as collected right now.
        SpiderFSM spider = other.GetComponent<SpiderFSM>();
        if (spider != null)
        {
            isFinished = true;
 
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpiderShot();
            }
 
            Destroy(spider.gameObject);
            Destroy(gameObject);
            return;
        }
 
        // Anything else solid (a wall or an obstacle): burst.
        StartCoroutine(Burst());
    }
 
    // Grows the projectile and fades it out, then removes it.
    private IEnumerator Burst()
    {
        isFinished = true;
 
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
 
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * 2.5f;
        Color startColor = spriteRenderer.color;
 
        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;
 
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
 
            Color faded = startColor;
            faded.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = faded;
 
            yield return null;
        }
 
        Destroy(gameObject);
    }
}