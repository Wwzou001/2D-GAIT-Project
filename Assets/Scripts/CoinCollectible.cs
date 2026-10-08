using System;
using UnityEngine;

public class CoinCollectible : MonoBehaviour
{
    // Raised when the player picks up a coin. Anything that wants to react
    // (a HUD counter, a sound, a score) can subscribe, so this script no
    // longer needs GameManager to have a CollectCoin() method.
    public static event Action<CoinCollectible> CoinCollected;

    private bool collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        if (!other.CompareTag("Player"))
            return;

        collected = true;

        CoinCollected?.Invoke(this);

        Destroy(gameObject);
    }
}