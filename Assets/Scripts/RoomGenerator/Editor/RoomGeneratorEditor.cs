using UnityEngine;
using UnityEditor;
 
// Adds an actual clickable "Generate Room" button under RoomGenerator's
// normal Inspector fields. This file must live in a folder named "Editor"
// anywhere in your project (e.g. Assets/Scripts/Editor/), otherwise Unity
// will try to include it in the real game build and fail, since UnityEditor
// code only exists in the Editor, not in a built game.
[CustomEditor(typeof(RoomGenerator))]
public class RoomGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draws all the normal fields (Default Spec's toggles, prefab slots, etc.) as usual.
        DrawDefaultInspector();
 
        RoomGenerator generator = (RoomGenerator)target;
 
        GUILayout.Space(10);
 
        if (GUILayout.Button("Generate Room (uses Default Spec)", GUILayout.Height(30)))
        {
            generator.GenerateRoom();
 
            // Marks the scene as having unsaved changes, so the generated
            // objects actually get saved when you save the scene.
            EditorUtility.SetDirty(generator);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }
}