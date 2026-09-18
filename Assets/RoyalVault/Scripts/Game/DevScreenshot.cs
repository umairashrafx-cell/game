using System;
using System.Collections;
using UnityEngine;

namespace RoyalVault.Game
{
    /// <summary>
    /// Captures the game's own framebuffer to a PNG when launched with
    /// <c>-rvshot &lt;path&gt;</c>, waits a moment for the first frames to settle, then quits.
    ///
    /// Exists so the game can be inspected visually from the command line without capturing the
    /// whole desktop — which is both unreliable (the window may not be focused) and intrusive,
    /// since it photographs whatever else the machine's owner happens to be doing.
    ///
    /// Harmless in a normal launch: with no flag it removes itself on the first frame.
    /// </summary>
    public sealed class DevScreenshot : MonoBehaviour
    {
        private const string Flag = "-rvshot";

        public static void AttachIfRequested()
        {
            string path = ResolvePath();
            if (string.IsNullOrEmpty(path)) return;

            GameObject go = new GameObject("DevScreenshot");
            DontDestroyOnLoad(go);
            go.AddComponent<DevScreenshot>().StartCoroutine(
                go.GetComponent<DevScreenshot>().CaptureThenQuit(path));
        }

        private static string ResolvePath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private IEnumerator CaptureThenQuit(string path)
        {
            // Let the board build, the reflection probe render and the first animations settle.
            yield return new WaitForSecondsRealtime(2.5f);
            yield return new WaitForEndOfFrame();

            ScreenCapture.CaptureScreenshot(path);

            // CaptureScreenshot writes asynchronously, so give it time to flush before quitting.
            yield return new WaitForSecondsRealtime(2.0f);
            Application.Quit();
        }
    }
}
