using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ConstrainProportions))]
public class EditorConstrainProportions : PropertyDrawer
{
    bool constrain = true;

    float aspectRatio = 1f;
    GUIContent linkIcon = EditorGUIUtility.IconContent("d_linked");
    GUIContent unlinkedIcon = EditorGUIUtility.IconContent("d_unlinked");
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {


        if (property.propertyType == SerializedPropertyType.Vector2)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            Rect fieldRect = new(position.x, position.y, position.width, position.height);
            Rect toggleRect = new(position.x - 25, position.y, 20, 18);

            Vector2 current = property.vector2Value;

            //Draw fields with toggle for constrain proportions
            EditorGUI.BeginChangeCheck();
            Vector2 newValue = EditorGUI.Vector2Field(fieldRect, GUIContent.none, current);
            if (EditorGUI.EndChangeCheck())
            {
                if (constrain)
                {
                    float deltaX = newValue.x - current.x;
                    float deltaY = newValue.y - current.y;

                    if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
                    {
                        newValue.y = current.y + deltaX / aspectRatio;
                    }
                    else
                    {
                        newValue.x = current.x + deltaY * aspectRatio;
                    }
                }
                else
                {
                    aspectRatio = newValue.x / newValue.y;
                }
            }

            //Draw toggle

            GUIContent activeIcon = constrain ? linkIcon : unlinkedIcon;
            constrain = GUI.Toggle(toggleRect, constrain, activeIcon, EditorStyles.iconButton);

            //Assign new value back to the property
            property.vector2Value = newValue;

            EditorGUI.EndProperty();
        }
        else
        {
            EditorGUI.LabelField(position, label.text, "Constrain Proportions for Vector2.");
        }
    }
}

public class ConstrainProportions : PropertyAttribute { }