using System.Collections;
using EntropyTag.UnityAdapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EntropyTag.Tests.PlayMode
{
    public sealed class BootstrapSceneTests
    {
        private const string BootstrapScenePath = "Assets/EntropyTag/Scenes/Bootstrap.unity";

        [UnityTest]
        public IEnumerator BootstrapSceneCreatesInitializedProjectRoot()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(BootstrapScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);

            while (!load.isDone)
            {
                yield return null;
            }

            ProjectBootstrap bootstrap = Object.FindObjectOfType<ProjectBootstrap>();

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.State, Is.Not.Null);
            Assert.That(bootstrap.State.ProductName, Is.EqualTo("EntropyTag"));

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!SceneManager.GetSceneByPath(ProjectBootstrap.FirstSliceScenePath).isLoaded &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetSceneByPath(ProjectBootstrap.FirstSliceScenePath).isLoaded, Is.True);
            yield return null;
            Assert.That(Object.FindObjectOfType<MatchFlowController>(), Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ProjectBootstrap.FirstSliceScenePath));
        }
    }
}
