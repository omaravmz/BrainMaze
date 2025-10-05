using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Escenas de laberinto (nombres EXACTOS en Build Settings)")]
    public string[] mazeScenes = { "Maze1", "Maze2", "Maze3" };

    [Header("Estado")]
    [SerializeField] private GameState state = GameState.Boot;
    [SerializeField] private int levelIndex = 0;

    // (Opcional) referencia al Player para calibrar desde UI
    [SerializeField] private PlayerMotor player;

    // Lectura para HUD/Debug
    public GameState State => state;
    public int LevelIndex => levelIndex;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Arranca en el primer laberinto
        LoadMaze(0);
    }

    void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Reengancha player al cambiar de escena
        if (player == null) player = FindAnyObjectByType<PlayerMotor>();
        // Al entrar a un laberinto, estamos jugando
        if (IsMazeScene(s.name)) SetState(GameState.MazePlaying);
    }

    // ---------- API PRINCIPAL ----------

    public void LoadMaze(int i)
    {
        if (mazeScenes == null || mazeScenes.Length == 0) { Debug.LogError("GameManager: no hay escenas configuradas."); return; }
        levelIndex = Mathf.Clamp(i, 0, mazeScenes.Length - 1);
        SetState(GameState.MazePlaying);
        SceneManager.LoadScene(mazeScenes[levelIndex]);
    }

    // Llamado por la "puerta/meta" del laberinto (DoorTrigger)
    public void OnMazeGoalReached()
    {
        SetState(GameState.MazeComplete);
        int next = levelIndex + 1;
        if (next < mazeScenes.Length) LoadMaze(next);
        else
        {
            // Último laberinto completo: por ahora reinicia el primero
            LoadMaze(0);
        }
    }

    public void TogglePause()
    {
        if (state == GameState.Paused)
            SetState(GameState.MazePlaying);
        else if (state == GameState.MazePlaying)
            SetState(GameState.Paused);
    }

    public void Calibrate()
    {
        if (player == null) player = FindAnyObjectByType<PlayerMotor>();
        player?.CalibrateNeutral();
    }

    // ---------- Helpers ----------

    private void SetState(GameState s)
    {
        state = s;
        Time.timeScale = (state == GameState.Paused) ? 0f : 1f;
    }

    private bool IsMazeScene(string sceneName)
    {
        if (mazeScenes == null) return false;
        foreach (var n in mazeScenes) if (n == sceneName) return true;
        return false;
    }
}
