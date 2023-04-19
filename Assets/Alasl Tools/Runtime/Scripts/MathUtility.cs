using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlaslTools
{

    public static class MathUtility
    {
        public static Vector3 Floor(Vector3 vec)
        {
            return new Vector3(
                Mathf.Floor(vec.x),
                Mathf.Floor(vec.y),
                Mathf.Floor(vec.z));
        }

        public static Vector3 Round(Vector3 vec)
        {
            return new Vector3(
                Mathf.Round(vec.x),
                Mathf.Round(vec.y),
                Mathf.Round(vec.z));
        }

        public static Vector3 Abs(Vector3 vec)
        {
            return new Vector3(
                Mathf.Abs(vec.x),
                Mathf.Abs(vec.y),
                Mathf.Abs(vec.z));
        }
    }

}