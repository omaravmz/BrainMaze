using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private float delayNextSec = 0.5f;

    public void LoadNext()
    {
        int total = SceneManager.sceneCountInBuildSettings;
        if (total == 0) return;

        int idx = SceneManager.GetActiveScene().buildIndex;
        int next = (idx + 1) % total;

        SceneManager.LoadScene(next);
        // <- ya no llamamos OnSceneLoadedHook(); GameManager se reengancha solo por evento
    }

    public void Reload()
    {
        int idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);
        // <- igual, sin hook manual
    }

    public void CompleteAndNext()
    {
        GameManager.I?.OnMazeGoalReached();
        Invoke(nameof(LoadNext), delayNextSec);
    }
}
