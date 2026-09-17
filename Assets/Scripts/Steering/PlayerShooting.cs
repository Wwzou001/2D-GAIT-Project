using UnityEngine;
using UnityEngine.InputSystem;

// Right-click fires a projectile from the player toward the mouse position.
// Left-click stays free for SeekArrive movement targeting.
public class PlayerShooting : MonoBehaviour
{
    [Header("Projectile Settings")]
    public Sprite projectileSprite;
    public Color projectileColor = Color.white;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 3f;
    public float projectileColliderRadius = 0.15f;

    [Header("Firing")]
    public float fireCooldown = 0.3f;
    private float nextFireTime = 0f;

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireCooldown;
        }
    }

    private void Fire()
    {
        if (projectileSprite == null)
        {
            Debug.LogWarning("PlayerShooting: no projectile sprite assigned in the Inspector.");
            return;
        }

        Vector3 screenPos = Mouse.current.position.ReadValue();
        Vector2 targetPos = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2 direction = (targetPos - (Vector2)transform.position).normalized;

        GameObject go = new GameObject("Projectile");
        go.transform.position = transform.position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = projectileSprite;
        sr.color = projectileColor;
        sr.sortingOrder = 10;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = projectileColliderRadius;

        Projectile projectile = go.AddComponent<Projectile>();
        projectile.speed = projectileSpeed;
        projectile.lifetime = projectileLifetime;
        projectile.SetDirection(direction);
    }
}
