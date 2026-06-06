using UnityEngine;
using UnityEngine.UI;

public class PackagePickupDebugUI : MonoBehaviour
{
    public PackagePickup packagePickup;

    void Start()
    {
        GameObject canvasGO = new GameObject("PackagePickupDebugCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject buttonGO = new GameObject("ResetPackageButton");
        buttonGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = buttonGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(200f, 60f);

        Image bg = buttonGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        Button button = buttonGO.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        button.colors = colors;
        button.onClick.AddListener(packagePickup.ResetPackage);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(buttonGO.transform, false);

        Text label = textGO.AddComponent<Text>();
        label.text = "Reset Package";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        RectTransform labelRT = textGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
    }
}
