using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(Resolution))]
public class EditorRestrictResolution : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        property.intValue = Mathf.Clamp(EditorGUI.IntField(position, label, property.intValue), 8, 1024);

        if (property.intValue % 8 != 0)
        {
            property.intValue = Mathf.RoundToInt(property.intValue / 8.0f) * 8;
        }
    }
}

