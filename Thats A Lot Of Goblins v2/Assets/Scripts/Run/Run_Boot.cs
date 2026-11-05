using UnityEngine;
using UnityEngine.SceneManagement;

// Starts the run: loads the first area on top of the Run scene and makes it the active scene (so its lighting settings
// apply). Later stages call LoadArea for the next one.
//
// In the editor, pressing Play with an area scene open starts here anyway (Area_PlayMode) and loads that area, so any
// area can be tested on its own. Both scenes must be in Build Settings, the Run scene first.
//
// Setup: put it on an object in the Run scene and assign First Area Scene (used in builds, and when you press Play in
// the Run scene itself).
//
// GC: none per frame. No subscriptions. One async scene load.
public class Run_Boot : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("The area a run starts in (and the one Play in this scene loads).")]
    [SerializeField] private UnityEditor.SceneAsset firstAreaScene;
#endif

    [SerializeField, HideInInspector] private string firstAreaPath;

    // Set by Area_PlayMode when Play is pressed with an area open
    public const string EditorStartKey = "Run_StartArea";

    // An area scene is still loading: Run_Edge keeps the screen covered until it's done
    public static bool Loading { get; private set; }

    private void OnValidate()
    {
#if UNITY_EDITOR
        firstAreaPath = firstAreaScene != null ? UnityEditor.AssetDatabase.GetAssetPath(firstAreaScene) : string.Empty;
#endif
    }

    private void Start()
    {
        // Both scenes already open in the editor: the area is there, nothing to load
        if (Area_Root.Current != null)
        {
            SceneManager.SetActiveScene(Area_Root.Current.gameObject.scene);
            return;
        }

        string path = firstAreaPath;

#if UNITY_EDITOR
        string chosen = UnityEditor.SessionState.GetString(EditorStartKey, string.Empty);

        if (!string.IsNullOrEmpty(chosen))
            path = chosen;
#endif

        // Moving on from an area: the next one wins
        if (Run_Carry.Has && !string.IsNullOrEmpty(Run_Carry.AreaPath))
            path = Run_Carry.AreaPath;

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"{name}: Run_Boot has no First Area Scene.", this);
            return;
        }

        LoadArea(path);
    }

    public void LoadArea(string path)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
        Loading = load != null;

        if (load == null)
        {
            Debug.LogError($"{name}: couldn't load '{path}'. Add it to Build Settings (Run scene first).", this);
            return;
        }

        load.completed += _ =>
        {
            Loading = false;
            Scene loaded = SceneManager.GetSceneByPath(path);

            if (loaded.IsValid())
                SceneManager.SetActiveScene(loaded);
        };
    }
}
