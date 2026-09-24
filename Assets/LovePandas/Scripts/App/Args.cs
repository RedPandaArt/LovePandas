using System;

namespace LovePandas.App
{
    /// Аргументы командной строки вида `-имя значение` — для тестовых запусков на ПК.
    public static class Args
    {
        public static string Get(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
