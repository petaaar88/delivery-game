using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RCC_CarControllerV4))]
public class VehiclePickupAnimator : MonoBehaviour
{
    [Header("Doors")]
    public Transform rightDoor;
    public Transform leftDoor;
    [Range(45f, 135f)]
    public float doorOpenAngle = 90f;
    public float doorAnimDuration = 0.35f;

    [Header("Package")]
    public Transform bouncePoint;
    public Transform packageSlot;
    public float archHeight = 2f;
    public float packageFlyDuration = 1.0f;
    public float packageBounceDuration = 0.7f;

    private RCC_CarControllerV4 _car;

    void Awake()
    {
        _car = GetComponent<RCC_CarControllerV4>();
    }

    public void StartPickupSequence(Transform package)
    {
        StartCoroutine(PickupSequence(package));
    }

    IEnumerator PickupSequence(Transform package)
    {
        _car.canControl = false;

        yield return StartCoroutine(AnimateDoors(true));
        yield return StartCoroutine(FlyPackage(package));
        yield return StartCoroutine(AnimateDoors(false));

        package.gameObject.SetActive(false);
        _car.canControl = true;
    }

    IEnumerator AnimateDoors(bool open)
    {
        bool leftFirst = Random.value < 0.5f;
        Transform first = leftFirst ? leftDoor : rightDoor;
        Transform second = leftFirst ? rightDoor : leftDoor;
        float firstAngle = leftFirst ? (open ? doorOpenAngle : 0f) : (open ? -doorOpenAngle : 0f);
        float secondAngle = leftFirst ? (open ? -doorOpenAngle : 0f) : (open ? doorOpenAngle : 0f);

        yield return StartCoroutine(AnimateSingleDoor(first, firstAngle));
        yield return StartCoroutine(AnimateSingleDoor(second, secondAngle));
    }

    IEnumerator AnimateSingleDoor(Transform door, float targetYAngle)
    {
        float elapsed = 0f;
        Quaternion start = door.localRotation;
        Quaternion target = Quaternion.Euler(0f, targetYAngle, 0f);

        while (elapsed < doorAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / doorAnimDuration);
            door.localRotation = Quaternion.Lerp(start, target, t);
            yield return null;
        }

        door.localRotation = target;
    }

    IEnumerator FlyPackage(Transform package)
    {
        package.SetParent(null);

        yield return StartCoroutine(FlyArc(package, package.position, bouncePoint.position, archHeight, packageFlyDuration));
        yield return StartCoroutine(FlyArc(package, bouncePoint.position, packageSlot.position, archHeight * 0.5f, packageBounceDuration));
    }

    IEnumerator FlyArc(Transform obj, Vector3 from, Vector3 to, float height, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(from, to, smooth);
            pos.y += height * Mathf.Sin(smooth * Mathf.PI);
            obj.position = pos;
            yield return null;
        }

        obj.position = to;
    }
}
