using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Setup {
    public static void Create() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var camera = new GameObject("_MainCamera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0,0,-30);
        camera.orthographic = true;
        camera.orthographicSize = 10;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var light = new GameObject("Flight deck lighting").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(25,-25,0);
        light.intensity = 1.3f;
        var game = new GameObject("Space SHMUP").AddComponent<SpaceShmup>();
        const string root = "Assets/Assets by Peter Burroughs/Models/";
        game.heroModel = AssetDatabase.LoadAssetAtPath<GameObject>(root + "heroShip.fbx");
        game.enemies = new GameObject[5];
        for (int i=0; i<5; i++) game.enemies[i] = AssetDatabase.LoadAssetAtPath<GameObject>(root + "enemy" + i + ".fbx");
        game.stars = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Materials/Space.png");
        EditorSceneManager.SaveScene(scene, "Assets/Space SHMUP.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(scene.path,true)};
    }
}
