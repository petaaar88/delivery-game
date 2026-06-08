using UnityEngine;

public class ExplosiveCollisionDetector : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Explosive package — collided with {collision.gameObject.name}! Impulse: {collision.impulse.magnitude:F1} N");
    }
}
