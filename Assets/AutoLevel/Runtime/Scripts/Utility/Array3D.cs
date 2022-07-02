using System;
using UnityEngine;

[Serializable]
public class Array3D<T>
{
    public Vector3Int Size => size;

    [SerializeField]
    private Vector3Int size;
    [SerializeField]
    private T[] array;

    private int xy => size.x * size.y;

    public Array3D() : this(Vector3Int.zero) { }

    public Array3D(Vector3Int size)
    {
        this.size = size;
        array = new T[xy * size.z];
    }

    public T this[int k, int j, int i]
    {
        get => array[i + j * size.x + k * xy];
        set => array[i + j * size.x + k * xy] = value;
    }
}