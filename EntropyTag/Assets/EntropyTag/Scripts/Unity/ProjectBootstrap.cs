using EntropyTag.Application;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EntropyTag.UnityAdapters
{
    public sealed class ProjectBootstrap : MonoBehaviour
    {
        public const string FirstSliceScenePath = "Assets/EntropyTag/Scenes/Gameplay/Arena_FirstSlice.unity";

        public StartupState State { get; private set; }

        private void Awake()
        {
            State = StartupState.CreateDefault();
        }

        private IEnumerator Start()
        {
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(FirstSliceScenePath))
            {
                throw new InvalidOperationException($"Required match scene is missing from the build: {FirstSliceScenePath}");
            }

            Scene arena = SceneManager.GetSceneByPath(FirstSliceScenePath);
            if (!arena.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(FirstSliceScenePath, LoadSceneMode.Additive);
                arena = SceneManager.GetSceneByPath(FirstSliceScenePath);
            }

            if (!arena.isLoaded || !SceneManager.SetActiveScene(arena))
            {
                throw new InvalidOperationException("The first-slice match scene could not become active.");
            }
        }
    }
}
