using UnityEngine;

public class ExplosivePackageEffect : MonoBehaviour, IPackageEffect
{
    private ExplosiveCollisionDetector _detector;

    public void Activate(RCC_CarControllerV4 car)
    {
        _detector = car.gameObject.AddComponent<ExplosiveCollisionDetector>();
        Debug.Log("Explosive package loaded — collision detection active.");
    }

    public void Deactivate()
    {
        if (_detector != null)
            Destroy(_detector);
        Debug.Log("Explosive package delivered safely.");
    }
}
