using UnityEngine;

namespace AlaslTools
{

    public static class ColorUtility
    {
        public static Color GetColor(int hash)
        {
            return new Color(
                (hash & 255) / 255f,
                ((hash >> 8) & 255) / 255f,
                ((hash >> 16) & 255) / 255f);
        }

        public static Color SetAlpha(this Color color, float a)
        {
            color.a = a;
            return color;
        }
    }

}
