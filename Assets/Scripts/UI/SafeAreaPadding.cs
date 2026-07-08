using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Adds the device safe-area insets (notches, rounded corners) as extra padding
/// on the element named "safe-area", on top of its USS padding.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SafeAreaPadding : MonoBehaviour
{
    UIDocument _doc;
    Rect _applied = Rect.zero;

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != _applied)
            Apply();
    }

    void Apply()
    {
        _applied = Screen.safeArea;

        var root = _doc.rootVisualElement;
        var safeArea = root?.Q("safe-area");
        if (safeArea == null)
            return;

        var panel = root.panel;
        if (panel == null)
            return;

        // Screen-space insets converted to panel units. safeArea is in screen
        // pixels with origin bottom-left; panel origin is top-left.
        Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(
            panel, new Vector2(_applied.xMin, Screen.height - _applied.yMax));
        Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(
            panel, new Vector2(Screen.width - _applied.xMax, _applied.yMin));

        safeArea.style.marginLeft = topLeft.x;
        safeArea.style.marginTop = topLeft.y;
        safeArea.style.marginRight = bottomRight.x;
        safeArea.style.marginBottom = bottomRight.y;
    }
}
