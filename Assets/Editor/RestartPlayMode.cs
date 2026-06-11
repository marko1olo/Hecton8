using UnityEditor;
public static class RestartPlayMode {
    public static void DoRestart() {
        if (EditorApplication.isPlaying) {
            EditorApplication.isPlaying = false;
        } else {
            EditorApplication.isPlaying = true;
        }
    }
}
