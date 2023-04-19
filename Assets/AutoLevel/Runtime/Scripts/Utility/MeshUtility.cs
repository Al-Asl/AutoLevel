using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoLevel
{

    public static class MeshUtility
    {
        public static void GetMeshData(Transform transform, out Mesh mesh,
            out List<Vector3> verts)
        {
            mesh = transform.GetComponent<MeshFilter>().sharedMesh;
            if (!mesh.isReadable)
            {
                throw new System.Exception("the mesh isn't readable");
            }
            verts = new List<Vector3>();
            mesh.GetVertices(verts);
        }

        public static void GetMeshData(Transform transform, out Mesh mesh,
        out List<Vector3> verts, out List<Vector3> norms)
        {
            GetMeshData(transform, out mesh, out verts);
            norms = new List<Vector3>();
            mesh.GetNormals(norms);
        }

        public static void GetMeshData(Transform transform, out Mesh mesh,
            out List<Vector3> verts, out List<int> indices)
        {
            GetMeshData(transform, out mesh, out verts);
            indices = new List<int>();
            mesh.GetIndices(indices, 0);
        }

        public static void GetMeshData(Transform transform, out Mesh mesh,
        out List<Vector3> verts, out List<Vector3> norms, out List<int> indices)
        {
            GetMeshData(transform, out mesh, out verts, out norms);
            indices = new List<int>();
            mesh.GetIndices(indices, 0);
        }

        public static void Flip(Mesh mesh)
        {
            var norms = new List<Vector3>();
            mesh.GetNormals(norms);
            InverseNormals(norms);
            mesh.SetNormals(norms);
            FlipFaces(mesh);
            mesh.RecalculateBounds();
        }

        public static void InverseNormals(List<Vector3> norms)
        {
            for (int i = 0; i < norms.Count; i++)
                norms[i] = -norms[i];
        }

        public static void Rotate(Mesh mesh, Vector3 pivot, float angle, Axis axis)
        {
            TransformMesh(mesh, Matrix4x4.identity.Rotate(pivot, angle, axis));
        }

        public static void Rotate(List<Vector3> verts, List<Vector3> norms,
            Vector3 pivot, float angle, Axis axis)
        {
            TransformMesh(verts, norms, Matrix4x4.identity.Rotate(pivot, angle, axis));
        }

        public static void Mirror(List<Vector3> verts, List<Vector3> norms, Vector3 pivot, Axis axis)
        {
            TransformMesh(verts, norms, Matrix4x4.identity.Mirorr(pivot, axis));
        }

        public static void Mirror(Mesh mesh, Vector3 pivot, Axis axis)
        {
            FlipFaces(mesh);
            TransformMesh(mesh, Matrix4x4.identity.Mirorr(pivot, axis));
        }

        public static void TransformMesh(Mesh mesh, Matrix4x4 transform)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            mesh.GetVertices(verts);
            mesh.GetNormals(norms);

            TransformMesh(verts, norms, transform);

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.RecalculateBounds();
        }

        public static void TransformMesh(List<Vector3> verts, List<Vector3> norms, Matrix4x4 transform)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = transform.MultiplyPoint(verts[i]);
                norms[i] = transform.MultiplyVector(norms[i]);
            }
        }

        private static void FlipFaces(Mesh mesh)
        {
            var Indices = mesh.GetIndices(0);
            for (int i = 0; i < Indices.Length; i += 3)
            {
                var temp = Indices[i];
                Indices[i] = Indices[i + 2];
                Indices[i + 2] = temp;
            }
            mesh.SetIndices(Indices, MeshTopology.Triangles, 0);
        }

        public static bool RayTriangleIntersect(Ray ray, Vector3 a, Vector3 b, Vector3 c
        , ref float t, ref Vector3 bary, ref Vector3 normal)
        {
            Vector3 ba = b - a;
            Vector3 ca = c - a;

            normal = Vector3.Cross(ba, ca);
            float areaABC = normal.magnitude;
            normal.Normalize();

            float ndr = Vector3.Dot(normal, ray.direction);
            if (Mathf.Abs(ndr) < 0.00001f)
                return false;

            float d = Vector3.Dot(normal, a);
            t = (-Vector3.Dot(ray.origin, normal) + d) / ndr;
            if (t < 0) return false;

            Vector3 p = ray.GetPoint(t);

            float areaPBC = Vector3.Dot(normal, Vector3.Cross((b - c), (b - p)));
            bary.x = areaPBC / areaABC;
            if (bary.x < 0)
                return false;

            float areaPCA = Vector3.Dot(normal, Vector3.Cross((c - a), (c - p)));
            bary.y = areaPCA / areaABC;
            if (bary.y < 0)
                return false;

            bary.z = 1.0f - bary.x - bary.y;
            if (bary.z < 0)
                return false;

            return true;
        }
    }

}