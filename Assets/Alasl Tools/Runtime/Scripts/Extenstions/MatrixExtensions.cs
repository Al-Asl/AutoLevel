using UnityEngine;

namespace AlaslTools
{
    public enum Axis
    {
        x, y, z
    }

    public static class MatrixExtensions
    {
        public static Matrix4x4 Rotate(this Matrix4x4 matrix, Vector3 pivot, float angle, Axis axis)
        {
            Vector3 rotation = Vector3.zero;
            rotation[(int)axis] = angle;
            return Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(rotation)) * Matrix4x4.Translate(-pivot) * matrix;
        }
        public static Matrix4x4 Mirorr(this Matrix4x4 matrix, Vector3 pivot, Axis axis)
        {
            Vector3 scale = Vector3.one;
            scale[(int)axis] = -1;
            return Matrix4x4.Translate(pivot) * Matrix4x4.Scale(scale) * Matrix4x4.Translate(-pivot) * matrix;
        }
    }
}