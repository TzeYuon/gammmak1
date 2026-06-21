using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneId
{
    BootstrapScene,
    MainMenuScene,
    GameplayScene,
}

/// <summary>
/// Controls execution flow of the game at the higher scene level.
/// Handles initial setup and exiting the game.
/// </summary>
public class SceneManager : MonoBehaviour
{
    public static SceneManager Instance { get; private set; }

    [SerializeField] UIScreenManager uiScreenManager;

    SceneId curScene;
    SceneId _prevId;
    public EventHandler<SceneChangeArgs> OnSceneChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

            InitScenes();
    }

    void InitScenes()
    {
        foreach (SceneId id in Enum.GetValues(typeof(SceneId)))
        {
            if (!DoesSceneExist($"{id}"))
            {
                Debug.LogWarning($"{id} does not have a valid scene in build settings");
            }
        }

        static bool DoesSceneExist(string name)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var lastSlash = scenePath.LastIndexOf("/");
                var sceneName = scenePath.Substring(lastSlash + 1, scenePath.LastIndexOf(".") - lastSlash - 1);

                if (string.Compare(name, sceneName, true) == 0)
                    return true;
            }

            return false;
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        LoadScene(SceneId.MainMenuScene);
    }

    public void LoadScene(SceneId sceneToLoad)
    {
        if (curScene != SceneId.BootstrapScene) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(curScene.ToString());
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad.ToString(), LoadSceneMode.Additive);
    }

    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (loadedScene.name.Contains("Subscene"))
        {
            return;
        }

        if (Enum.TryParse(loadedScene.name, out SceneId id))
        {
            OnSceneChanged?.Invoke(this, new() { prevSceneId = _prevId, newSceneId = id });
            curScene = id;
            _prevId = id;
        }
        else
        {
            Debug.LogWarning($"Scene: {loadedScene.name} does not exist in SceneId");
        }
    }

    public void CloseApplication()
    {
        Application.Quit();
    }
}

public class SceneChangeArgs
{
    public SceneId prevSceneId;
    public SceneId newSceneId;
}