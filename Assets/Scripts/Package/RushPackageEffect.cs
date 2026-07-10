using UnityEngine;

public class RushPackageEffect : MonoBehaviour, IPackageEffect
{
    const float DefaultRushDuration = 30f;

    [Tooltip("Seconds available to deliver this rush package. The global run timer keeps counting down too.")]
    [Min(1f)]
    public float rushDuration = DefaultRushDuration;

    public void Activate(RCC_CarControllerV4 car)
    {
        float duration = rushDuration > 0f ? rushDuration : DefaultRushDuration;
        GameSession.Instance?.BeginRushPackage(duration);
        Debug.Log("Rush package picked up — deliver fast!");
    }

    public void Deactivate()
    {
        GameSession.Instance?.EndRushPackage(false);
        Debug.Log("Rush package delivered.");
    }
}
