using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastLight.Presentation
{
    /// <summary>
    /// The title screen. Starts a run or quits.
    /// </summary>
    /// <remarks>
    /// This is the one place the game changes scenes. Everything inside a run happens by swapping
    /// panels instead, so the run's state never has to survive a scene load - which is why
    /// starting a new run from the result screen is an in-place reset rather than a reload.
    /// </remarks>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private Button beginButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string gameSceneName = "Game";

        private void Awake()
        {
            if (beginButton != null) beginButton.onClick.AddListener(BeginRun);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        public void BeginRun() => SceneManager.LoadScene(gameSceneName);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        public void Bind(Button begin, Button quit, string sceneName)
        {
            beginButton = begin;
            quitButton = quit;
            gameSceneName = sceneName;
        }
#endif
    }
}
