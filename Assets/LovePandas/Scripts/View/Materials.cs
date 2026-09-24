using System.Collections.Generic;
using UnityEngine;

namespace LovePandas.View
{
    /// Кэш цветных материалов. База — Resources/Materials/Base.mat (URP Lit): на ассет есть ссылка,
    /// поэтому шейдер попадает в сборку. Стандартный материал примитива в сборке пурпурный.
    public static class Materials
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Material basis;

        public static Material Lit(Color color)
        {
            if (cache.TryGetValue(color, out var m) && m != null) return m;
            if (basis == null) basis = Resources.Load<Material>("Materials/Base");
            m = new Material(basis);
            m.SetColor("_BaseColor", color);
            cache[color] = m;
            return m;
        }
    }
}
