using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

namespace AlaslTools
{
    public static class HandleEx
    {
        static class WorldRayCastManager
        {
            static Dictionary<int, float> controls = new Dictionary<int, float>();
            static int id;

            static WorldRayCastManager()
            {
                SceneView.beforeSceneGui += BeforeSceneGUI;
            }

            static void BeforeSceneGUI(SceneView view)
            {
                id = -1;
                var minDist = float.MaxValue;
                foreach (var pair in controls)
                {
                    if (pair.Value < minDist)
                    {
                        id = pair.Key;
                        minDist = pair.Value;
                    }
                }
                controls.Clear();
            }

            public static void Submit(float distance, int id)
            {
                controls[id] = distance;
            }

            public static bool Check(int id)
            {
                return id == WorldRayCastManager.id;
            }

        }

        #region LayoutFunctions

        public interface ILayoutFromDraw
        {
            float Distance(DrawCommand command, int id);
        }

        public struct QuadD : ILayoutFromDraw
        {
            public float Distance(DrawCommand command, int id)
            {
                var matrix = command.matrix;
                return HandleUtility.DistanceToRectangle(
                matrix.MultiplyPoint(Vector3.zero), matrix.rotation, matrix.lossyScale.x * 0.5f);
            }
        }

        public struct SphereD : ILayoutFromDraw
        {
            public float Distance(DrawCommand command, int id)
            {
                var matrix = command.matrix;
                return HandleUtility.DistanceToCircle(matrix.MultiplyPoint(Vector3.zero), matrix.lossyScale.x * 0.5f);
            }
        }

        public struct CubeD : ILayoutFromDraw
        {
            public float Distance(DrawCommand command, int id)
            {
                var matrix = command.matrix;
                return HandleUtility.DistanceToCube(matrix.MultiplyPoint(Vector3.zero), Quaternion.identity, matrix.lossyScale.x);
            }
        }

        public struct Cube3DD : ILayoutFromDraw
        {
            public float Distance(DrawCommand command, int id)
            {
                var matrix = command.matrix;
                var bounds = new Bounds(matrix.MultiplyPoint(Vector3.zero), matrix.lossyScale);
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (bounds.IntersectRay(ray, out var dist))
                    WorldRayCastManager.Submit(dist, id);
                return WorldRayCastManager.Check(id) ? 0 : float.MaxValue;
            }
        }

        public struct Quad3DD : ILayoutFromDraw
        {


            public static Vector3[] GetPlanePoints(Matrix4x4 matrix)
            {
                Vector3[] points = new Vector3[]
                {
                new Vector3(-0.5f,-0.5f,0),
                new Vector3(-0.5f,0.5f,0),
                new Vector3(0.5f,0.5f,0),
                new Vector3(0.5f,-0.5f,0),
                };

                for (int i = 0; i < points.Length; i++)
                    points[i] = matrix.MultiplyPoint3x4(points[i]);

                return points;
            }

            public float Distance(DrawCommand command, int id)
            {
                var matrix = command.matrix;

                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                float dist = 0f; Vector3 normal = default;
                if (ray.RayIntersectQuad(GetPlanePoints(matrix), ref dist, ref normal))
                {
                    if (Vector3.Dot(-matrix.GetColumn(2), normal) > 0)
                        WorldRayCastManager.Submit(dist, id);
                }

                return WorldRayCastManager.Check(id) ? 0 : float.MaxValue;
            }
        }

        #endregion

        public abstract class BaseHandle
        {
            public Event ce => Event.current;

            public DrawCommand normal = GetDrawCmd();
            public DrawCommand hover = GetDrawCmd();
            public DrawCommand active = GetDrawCmd();

            public void SetAll(DrawCommand command)
            {
                normal = command;
                hover = command;
                active = command;
            }

            public void SetAllTransformation(DrawCommand commnad)
            {
                normal.matrix = commnad.matrix;
                hover.matrix = commnad.matrix;
                active.matrix = commnad.matrix;
            }

            protected Camera camera => SceneView.lastActiveSceneView.camera;
            protected bool nearest => HandleUtility.nearestControl == id;
            protected bool selected => GUIUtility.hotControl == id;
            protected abstract string name { get; }
            protected int id;

            protected void ProcessGUI<T>() where T : ILayoutFromDraw, new()
            {
                id = GUIUtility.GetControlID(name.GetHashCode(), FocusType.Passive);
                float distance = default(T).Distance(normal, id);
                if (distance == 0 && ce.type == EventType.MouseMove)
                    SceneView.lastActiveSceneView.Repaint();

                switch (ce.type)
                {
                    case EventType.Repaint:
                        Repaint();
                        break;

                    case EventType.Layout:
                        HandleUtility.AddControl(id, distance);
                        break;
                }
            }

            protected void Repaint()
            {
                if (selected)
                    active.Draw();
                else if (nearest)
                    hover.Draw();
                else
                    normal.Draw();
            }
        }

        public class ButtonHandle : BaseHandle
        {
            protected override string name => "ButtonHandle";

            public bool Draw<T>() where T : ILayoutFromDraw, new()
            {
                ProcessGUI<T>();

                bool res = false;
                switch (ce.type)
                {
                    case EventType.MouseDown:
                        if (nearest && ce.button == 0 && !NavKeyDown())
                        {
                            res = true;
                            GUIUtility.hotControl = id;
                            GUIUtility.keyboardControl = id;
                            ce.Use();
                        }
                        break;

                    case EventType.MouseUp:
                        if (selected && ce.button == 0)
                        {
                            GUIUtility.hotControl = 0;
                            GUIUtility.keyboardControl = 0;
                            ce.Use();
                        }
                        break;
                }
                return res;
            }
        }

        public class DragHandle : BaseHandle
        {
            protected override string name => "DragHandle";
            public bool DidClick => didClick;
            private bool didClick;

            public bool Draw<T>() where T : ILayoutFromDraw, new()
            {
                ProcessGUI<T>();

                bool drag = false;
                didClick = false;
                switch (ce.type)
                {
                    case EventType.MouseUp:
                        if (selected && ce.button == 0)
                        {
                            GUIUtility.hotControl = 0;
                            GUIUtility.keyboardControl = 0;
                            ce.Use();
                        }
                        break;

                    case EventType.MouseDrag:
                        if (selected)
                        {
                            drag = true;
                            ce.Use();
                        }
                        break;

                    case EventType.MouseDown:
                        if (nearest && ce.button == 0 && !NavKeyDown())
                        {
                            didClick = true;
                            GUIUtility.hotControl = id;
                            GUIUtility.keyboardControl = id;
                            ce.Use();
                        }
                        break;
                }
                return drag;
            }
        }

        public struct DrawCommand
        {
            public Mesh mesh;
            public Material material;
            public Color color;
            public Texture texture;
            public Matrix4x4 matrix;

            public DrawCommand LookAtCamera()
            {
                var cameraTrans = SceneView.lastActiveSceneView.camera.transform;
                return LookAt(cameraTrans.forward, cameraTrans.up);
            }

            public DrawCommand LookAt(Vector3 dir, Vector3 up)
            {
                return Rotate(Quaternion.LookRotation(dir, up));
            }

            public DrawCommand LookAt(Vector3 dir)
            {
                return Rotate(Quaternion.LookRotation(dir));
            }

            public DrawCommand RotateAround(Vector3 point, Vector3 euler)
            {
                return RotateAround(point, Quaternion.Euler(euler));
            }

            public DrawCommand RotateAround(Vector3 point, Quaternion rot)
            {
                matrix = Matrix4x4.Translate(point) * Matrix4x4.Rotate(rot) * Matrix4x4.Translate(-point) * matrix;
                return this;
            }

            public DrawCommand Rotate(Quaternion rot)
            {
                matrix = Matrix4x4.Rotate(rot) * matrix;
                return this;
            }

            public DrawCommand Rotate(Vector3 euler)
            {
                matrix = Matrix4x4.Rotate(Quaternion.Euler(euler)) * matrix;
                return this;
            }

            public DrawCommand ConstantScreenSize(Vector3 pos)
            {
                matrix = matrix * Matrix4x4.Scale(Vector3.one * PixelInWorldUnit(pos));
                return this;
            }

            public DrawCommand Scale(float value)
            {
                matrix = Matrix4x4.Scale(Vector3.one * value) * matrix;
                return this;
            }

            public DrawCommand Scale(Vector3 vec)
            {
                matrix = Matrix4x4.Scale(vec) * matrix;
                return this;
            }

            public DrawCommand Move(Vector3 vec)
            {
                matrix = Matrix4x4.Translate(vec) * matrix;
                return this;
            }

            public DrawCommand SetMatrix(Matrix4x4 matrix)
            {
                this.matrix = matrix;
                return this;
            }

            public DrawCommand SetMaterial(Material material)
            {
                this.material = material;
                return this;
            }

            public DrawCommand SetMaterial(MaterialType materialType)
            {
                switch (materialType)
                {
                    case MaterialType.Opaque:
                        this.material = opaqueMaterial;
                        return this;
                    case MaterialType.Transparent:
                        this.material = transparentMaterial;
                        return this;
                    case MaterialType.UI:
                        this.material = uiMaterial;
                        return this;
                    case MaterialType.OpaqueShaded:
                        this.material = shadedOpaqueMaterial;
                        return this;
                }
                throw new Exception("undefined material!");
            }

            public DrawCommand SetTexture(Texture texture)
            {
                this.texture = texture;
                return this;
            }

            public DrawCommand SetColor(Color color)
            {
                this.color = color;
                return this;
            }

            public DrawCommand SetPrimitiveMesh(PrimitiveType primitive)
            {
                switch (primitive)
                {
                    case PrimitiveType.Sphere:
                        mesh = sphereMesh;
                        break;
                    case PrimitiveType.Capsule:
                        mesh = capsuleMesh;
                        break;
                    case PrimitiveType.Cylinder:
                        mesh = cylinderMesh;
                        break;
                    case PrimitiveType.Cube:
                        mesh = cubeMesh;
                        break;
                    case PrimitiveType.Plane:
                        mesh = planeMesh;
                        break;
                    case PrimitiveType.Quad:
                        mesh = quadMesh;
                        break;
                }
                return this;
            }

            public DrawCommand SetMesh(Mesh mesh)
            {
                this.mesh = mesh;
                return this;
            }

            public DrawCommand Copy()
            {
                return this;
            }

            public DrawCommand Draw(int pass = 0)
            {
                material.SetColor("_Color", color);
                material.SetTexture("_MainTex", texture);

                if (Event.current.type == EventType.Repaint)
                {
                    material.SetPass(pass);
                    Graphics.DrawMeshNow(mesh, matrix);
                }

                return this;
            }
        }

        public enum MaterialType
        {
            Opaque,
            Transparent,
            UI,
            OpaqueShaded
        }

        public enum MaterialPreset
        {
            Opaque,
            Transparent,
            UI
        }

        public enum ZTest
        {
            Always = 0,
            Less = 2,
            Equal = 3,
            LEqual = 4,
            GEqual = 5,
        }

        private static Mesh sphereMesh;
        private static Mesh cylinderMesh;
        private static Mesh capsuleMesh;
        private static Mesh quadMesh;
        private static Mesh cubeMesh;
        private static Mesh planeMesh;

        private static Material uiMaterial;
        private static Material transparentMaterial;
        private static Material opaqueMaterial;
        private static Material shadedOpaqueMaterial;

        public static Material OutLineMaterial;
        public static Material GridMaterial;


        public static ButtonHandle Button { get; private set; }
        public static DragHandle Drag { get; private set; }

        static HandleEx()
        {
            quadMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Quad);
            sphereMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Sphere);
            cylinderMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Cylinder);
            capsuleMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Capsule);
            cubeMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Cube);
            planeMesh = GameObjectUtil.GetPrimitiveMesh(PrimitiveType.Plane);

            uiMaterial = SetMaterialPreset(MaterialPreset.UI, new Material(Shader.Find("Hidden/AlaslTools/Handle/Standard")));
            uiMaterial.hideFlags = HideFlags.HideAndDontSave;
            opaqueMaterial = SetMaterialPreset(MaterialPreset.Opaque, new Material(uiMaterial));
            opaqueMaterial.hideFlags = HideFlags.HideAndDontSave;
            transparentMaterial = SetMaterialPreset(MaterialPreset.Transparent, new Material(uiMaterial));
            transparentMaterial.hideFlags = HideFlags.HideAndDontSave;
            shadedOpaqueMaterial = new Material(Shader.Find("Standard"));
            shadedOpaqueMaterial.hideFlags = HideFlags.HideAndDontSave;

            OutLineMaterial = new Material(Shader.Find("Hidden/AlaslTools/Outline"));
            OutLineMaterial.hideFlags = HideFlags.HideAndDontSave;
            GridMaterial = new Material(Shader.Find("Hidden/AlaslTools/Grid"));
            GridMaterial.hideFlags = HideFlags.HideAndDontSave;

            Button = new ButtonHandle();
            Drag = new DragHandle();
        }

        public static Material SetMaterialPreset(MaterialPreset preset, Material material, bool doubleSided = true)
        {
            material.SetFloat("_Cull", doubleSided ? 0 : 2);
            switch (preset)
            {
                case MaterialPreset.Opaque:
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZTest", (int)ZTest.LEqual);
                    material.SetFloat("_ZWrite", 1);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    return material;
                case MaterialPreset.Transparent:
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZTest", (int)ZTest.Less);
                    material.SetFloat("_ZWrite", 0);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    return material;
                case MaterialPreset.UI:
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZTest", (int)ZTest.Always);
                    material.SetFloat("_ZWrite", 0);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
                    return material;
            }
            throw new Exception("undefined preset!");
        }

        

        public static void DrawBounds(BoundsInt bounds, Color color)
        {
            DrawBounds(bounds.min, bounds.max, color);
        }

        public static void DrawBounds(Bounds bounds, Color color)
        {
            DrawBounds(bounds.min, bounds.max, color);
        }

        private static void DrawBounds(Vector3 min, Vector3 max, Color color)
        {
            Handles.color = color;

            Handles.DrawAAPolyLine(2f,
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, min.z));
            Handles.DrawAAPolyLine(2f,
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, min.y, max.z));
            Handles.DrawAAPolyLine(2f,
               new Vector3(min.x, min.y, min.z),
               new Vector3(min.x, min.y, max.z),
               new Vector3(max.x, min.y, max.z),
               new Vector3(max.x, min.y, min.z));
            Handles.DrawAAPolyLine(2f,
               new Vector3(min.x, max.y, min.z),
               new Vector3(min.x, max.y, max.z),
               new Vector3(max.x, max.y, max.z),
               new Vector3(max.x, max.y, min.z));
        }

        /// <summary>
        /// check for navigation keys 
        /// </summary>
        public static bool NavKeyDown()
        {
            return Event.current.alt || Event.current.button == 2 || Event.current.button == 1;
        }

        /// <summary>
        /// how many world unit to cover one pixel at a given position
        /// </summary>
        public static float PixelInWorldUnit(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position) * 0.01f;
        }

        /// <summary>
        /// distance to the nearest handle 
        /// </summary>
        public static float NearestDistance()
        {
            Type TypeHandleUtility = typeof(HandleUtility);
            var field = TypeHandleUtility.GetField("s_NearestDistance", BindingFlags.Static | BindingFlags.NonPublic);
            return (float)field.GetValue(null);
        }

        public static DrawCommand GetDrawCmd()
        {
            return new DrawCommand().
                SetMatrix(Matrix4x4.identity).
                SetPrimitiveMesh(PrimitiveType.Quad).
                SetMaterial(MaterialType.UI).
                SetColor(Color.white);
        }
    }

}