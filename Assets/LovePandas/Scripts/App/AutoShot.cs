
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
            var dir = Args.Get("-lpShot");
            if (dir == null) return;
            var s = new GameObject("AutoShot").AddComponent<AutoShot>();
            s.dir = dir;
            s.screens = (Args.Get("-lpScreens") ?? "home").Split(',');
            s.ui = ui;
            s.StartCoroutine(s.Run());
        }

        IEnumerator Run()
        {
            System.IO.Directory.CreateDirectory(dir);
            // дождаться подключения (экраны кроме онбординга открываются только с главного)
            float until = Time.time + 12f;
            while (!ui.IsHome && Time.time < until && screens[0] != "onboarding") yield return null;
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
    }
}
