using System;
using System.Collections.Generic;
using UnityEngine;

public static class MathUtility
{
    public static Vector3Int RoundToInt(Vector3 vec)
    {
        return new Vector3Int(
            Mathf.RoundToInt(vec.x),
            Mathf.RoundToInt(vec.y),
            Mathf.RoundToInt(vec.z));
    }

    public static Vector3Int FloorToInt(Vector3 vec)
    {
        return new Vector3Int(
            Mathf.FloorToInt(vec.x),
            Mathf.FloorToInt(vec.y),
            Mathf.FloorToInt(vec.z));
    }

    public static Vector3Int CeilToInt(Vector3 vec)
    {
        return new Vector3Int(
            Mathf.CeilToInt(vec.x),
            Mathf.CeilToInt(vec.y),
            Mathf.CeilToInt(vec.z));
    }

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
}