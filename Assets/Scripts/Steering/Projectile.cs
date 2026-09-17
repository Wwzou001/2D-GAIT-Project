using UnityEngine;

// A simple projectile: moves in a straight line, destroys any fly it touches,
// and destroys itself either on impact or after its lifetime runs out.
public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifetime = 3f;

    private Vector2 direction;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime); // clean up automatically if it never hits anything
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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

        // hit something solid that isn't a fly (a wall, an obstacle) - stop here
        Destroy(gameObject);
    }
}
