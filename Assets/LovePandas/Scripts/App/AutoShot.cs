using System;
using System.Collections;
using LovePandas.UI;
using UnityEngine;

namespace LovePandas.App
{
    /// Проверка без рук: `LovePandas.exe -lpShot D:\out -lpScreens home,board,shop,wardrobe`
    /// снимает скриншоты нужных экранов и закрывает игру. Нужен агенту, чтобы видеть результат.
    public class AutoShot : MonoBehaviour
    {
        string dir;
        string[] screens;
        AppUI ui;

        public static void TryStart(AppUI ui)
        {
            var dir = Arg("-lpShot");
            if (dir == null) return;
            var s = new GameObject("AutoShot").AddComponent<AutoShot>();
            s.dir = dir;
            s.screens = (Arg("-lpScreens") ?? "home").Split(',');
            s.ui = ui;
            s.StartCoroutine(s.Run());
        }

        IEnumerator Run()
        {
            System.IO.Directory.CreateDirectory(dir);
            foreach (var screen in screens)
            {
                ui.DebugOpen(screen);
                yield return new WaitForSeconds(1.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, screen + ".png"));
                yield return null;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
