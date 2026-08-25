using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dirichlet.Mediation.Samples
{
    /// <summary>
    /// Bootstrap helper that ensures the Dirichlet demo controller exists after scene load.
    /// </summary>
    internal static class DirichletDemoBootstrap
    {
        private const string DemoSceneName = "DirichletMediationDemo";
        private const string DemoSceneFileName = "DirichletMediationDemo.unity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDemo()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!IsDemoScene(SceneManager.GetActiveScene()))
            {
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<DirichletDemoController>() != null)
            {
                return;
            }

            var host = new GameObject("DirichletDemo");
            host.AddComponent<DirichletDemoController>();
        }

        private static bool IsDemoScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            if (string.Equals(scene.name, DemoSceneName, StringComparison.Ordinal))
            {
                return true;
            }

            var path = scene.path;
            return !string.IsNullOrEmpty(path) && path.EndsWith("/" + DemoSceneFileName, StringComparison.Ordinal);
        }
    }
}


