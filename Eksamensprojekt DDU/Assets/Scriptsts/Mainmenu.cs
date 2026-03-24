using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayButton : MonoBehaviour
{
    public void OnPlayButtonPressed()
    {
        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextScene < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.LogWarning("No next scene found. Check your Build Settings.");
        }
    }
    public void Quitgame ()
    {
        Debug.Log ("QUIT!");
        Application.Quit();

    }
}
