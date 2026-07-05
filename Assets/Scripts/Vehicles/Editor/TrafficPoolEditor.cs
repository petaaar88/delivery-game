using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="TrafficPool"/>: adds a "Refresh Routes From Scene" button
/// so the per-route cap list can be populated with one click.
/// </summary>
[CustomEditor(typeof(TrafficPool))]
public class TrafficPoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Refresh Routes From Scene"))
        {
            foreach (Object t in targets)
            {
                TrafficPool pool = (TrafficPool)t;
                Undo.RecordObject(pool, "Refresh Traffic Routes");
                pool.RefreshRoutesFromScene();
                EditorUtility.SetDirty(pool);
            }
        }

        EditorGUILayout.HelpBox(
            "Each route's cap limits how many cars may be live on it at once. " +
            "Routes left out of the list are unlimited (only the global Live Car Cap applies).",
            MessageType.None);
    }
}

/// <summary>
/// Draws each <see cref="TrafficPool.RouteQuota"/> as a single row: route name on the
/// left, its max-cars field on the right.
/// </summary>
[CustomPropertyDrawer(typeof(TrafficPool.RouteQuota))]
public class RouteQuotaDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty routeProp = property.FindPropertyRelative("route");

        // Populated rows collapse to one line; an empty row shows the full default UI.
        return routeProp.objectReferenceValue == null
            ? EditorGUI.GetPropertyHeight(property, label, true)
            : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty labelProp = property.FindPropertyRelative("label");
        SerializedProperty maxProp = property.FindPropertyRelative("maxLiveCars");
        SerializedProperty routeProp = property.FindPropertyRelative("route");

        // If the row hasn't been populated yet (no route), fall back to the default
        // fields so it can still be edited by hand.
        if (routeProp.objectReferenceValue == null)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        string name = string.IsNullOrEmpty(labelProp.stringValue) ? "(route)" : labelProp.stringValue;

        float fieldWidth = 60f;
        Rect nameRect = new Rect(position.x, position.y, position.width - fieldWidth - 4f, position.height);
        Rect maxRect = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, position.height);

        EditorGUI.LabelField(nameRect, name);

        int prev = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUI.PropertyField(maxRect, maxProp, GUIContent.none);
        EditorGUI.indentLevel = prev;
    }
}
