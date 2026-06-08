using UnityEngine;

public class StandardPackageEffect : MonoBehaviour, IPackageEffect
{
    public void Activate(RCC_CarControllerV4 car)
    {
        Debug.Log("Standard package picked up — no special effect.");
    }

    public void Deactivate()
    {
        Debug.Log("Standard package delivered.");
    }
}
