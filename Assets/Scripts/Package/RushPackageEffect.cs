using UnityEngine;

public class RushPackageEffect : MonoBehaviour, IPackageEffect
{
    public void Activate(RCC_CarControllerV4 car)
    {
        Debug.Log("Rush package picked up — deliver fast!");
    }

    public void Deactivate()
    {
        Debug.Log("Rush package delivered.");
    }
}
