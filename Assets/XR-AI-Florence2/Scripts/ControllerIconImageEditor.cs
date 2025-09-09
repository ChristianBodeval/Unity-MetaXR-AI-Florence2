#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PresentFutures.XRAI.UI;

[CustomEditor(typeof(ControllerIconImage))]
public class ControllerIconImageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var comp = (ControllerIconImage)target;

        // Folder + Refresh
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        comp.resourcesFolder = EditorGUILayout.TextField("Resources Folder", comp.resourcesFolder);

        if (GUILayout.Button("Refresh List"))
        {
            comp.RefreshSprites();
        }

        // Show dropdown if we have sprites
        if (comp._sprites == null || comp._sprites.Length == 0)
        {
            EditorGUILayout.HelpBox(
                $"No sprites found at Resources/{comp.resourcesFolder}. " +
                $"Make sure your files are under 'Assets/Resources/{comp.resourcesFolder}/' " +
                $"and imported as Sprite (2D and UI).",
                MessageType.Info);
        }
        else
        {
            var names = new string[comp._sprites.Length];
            var index = 0;

            for (int i = 0; i < comp._sprites.Length; i++)
            {
                names[i] = comp._sprites[i] ? comp._sprites[i].name : "<null>";
                if (names[i] == comp.selectedSpriteName) index = i;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sprite", EditorStyles.boldLabel);
            var newIndex = EditorGUILayout.Popup("Choose Icon", index, names);
            if (newIndex != index)
            {
                comp.selectedSpriteName = names[newIndex];
                comp.ApplySelected();
            }
        }

        // Target Image
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        comp.targetImage = (UnityEngine.UI.Image)EditorGUILayout.ObjectField(
            "UI Image", comp.targetImage, typeof(UnityEngine.UI.Image), true);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(comp);
        }
    }
}
#endif
