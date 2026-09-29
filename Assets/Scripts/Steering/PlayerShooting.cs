using UnityEngine;
using UnityEngine.InputSystem;
 
// Lets the player fire a flame with a key (Enter by default).
// The flame flies in the direction the player is facing, which is read
// from PlayerMovement.FacingDirection.
//
// Put this on the Player next to PlayerMovement, then assign:
//   Projectile Sprite: the flame or arrow picture
public class PlayerShooting : MonoBehaviour
{
    [Header("Input")]
    public float fireCooldown = 0.3f;      // seconds between shots
 
    [Header("Projectile")]
    public Sprite projectileSprite;
    public Color projectileColor = Color.white;
    public float projectileSpeed = 5f;
    public float projectileLifetime = 3f;
    public float projectileColliderRadius = 0.15f;
 
    private PlayerMovement playerMovement;
    private float nextFireTime = 0f;
    private Sprite fallbackSprite;

 
    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }
 
    private void Update()
    {
        // No shooting once the game has ended.
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        if (Mouse.current == null)
            return;

        if (Mouse.current.rightButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireCooldown;
        }
    }
 
    private void Fire()
    {
        bool useFallback = projectileSprite == null;

        if (useFallback && fallbackSprite == null)
        {
            fallbackSprite = MakeFallbackSprite(16);
        }

        // Shoot toward wherever the player clicked.
        Vector3 screenPos = Mouse.current.position.ReadValue();
        Vector2 clickPos = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2 direction = (clickPos - (Vector2)transform.position).normalized;
        GameObject go = new GameObject("Projectile");
        go.transform.position = transform.position;
 
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = projectileSprite;
        sr.color = projectileColor;
        sr.sortingOrder = 10;
 
        // Trigger collider so it detects hits without being pushed around by physics.
        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = projectileColliderRadius;
 
        // Unity only sends trigger events when one side has a Rigidbody2D.
        // Kinematic means physics forces do not move it.
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
 
        // Add Projectile last, because it looks for the SpriteRenderer when it wakes up.
        Projectile projectile = go.AddComponent<Projectile>();
        projectile.speed = projectileSpeed;
        projectile.lifetime = projectileLifetime;
        projectile.SetDirection(direction);
    }
    // A white circle about 0.4 of a cell wide, used when no sprite is assigned.
    private static Sprite MakeFallbackSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        float radius = size / 2f;
        Vector2 centre = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();

        // pixelsPerUnit = size / 0.4 makes the sprite 0.4 world units wide.
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.4f);
    }
}