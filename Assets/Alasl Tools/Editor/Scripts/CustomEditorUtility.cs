using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using Object = UnityEngine.Object;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace AlaslTools
{
    public class SOIgnoreAttribute : Attribute { }

    /// <summary>
    /// a fast way to create an interface to deal with serialize object,
    /// consider using it if you don't care about it's performance and multi editing.
    /// </summary>
    public abstract class BaseSO<T> : IDisposable where T : Object
    {
        public T target;
        public SerializedObject serializedObject;

        private Dictionary<string, FieldInfo> fields;

        protected BaseSO(Object target)
        {
            this.target = (T)target;
            serializedObject = new SerializedObject(target);
            Intialize();
        }

        protected BaseSO(SerializedObject serializedObject)
        {
            this.serializedObject = serializedObject;
            target = (T)serializedObject.targetObject;
            Intialize();
        }

        void Intialize()
        {
            Undo.undoRedoPerformed += Update;

            var myType = GetType();
            var allFileds = myType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            fields = new Dictionary<string, FieldInfo>();
            for (int i = 0; i < allFileds.Length; i++)
            {
                var f = allFileds[i];
                bool ignore = false;
                foreach(var attr in f.CustomAttributes)
                {
                    if(attr.AttributeType == typeof(SOIgnoreAttribute))
                    {
                        ignore = true;
                        break;
                    }
                }
                if (!ignore && !f.DeclaringType.Name.StartsWith("BaseSO`"))
                    fields.Add(f.Name, f);
            }

            OnIntialize();

            Update();
        }

        protected virtual void OnIntialize() { }

        public void UpdateField(string name)
        {
            serializedObject.Update();

            var field = fields[name];
            var value = CustomEditorUtility.GetValue(field.FieldType, serializedObject.FindProperty(name));
            field.SetValue(this, value);
        }

        public void Update()
        {
            serializedObject.Update();
            foreach (var pair in fields)
            {
                var value = CustomEditorUtility.GetValue(pair.Value.FieldType, serializedObject.FindProperty(pair.Value.Name));
                pair.Value.SetValue(this, value);
            }
            OnUpdate();
        }

        protected virtual void OnUpdate() { }

        public void ApplyField(string name)
        {
            var field = fields[name];
            var value = field.GetValue(this);
            CustomEditorUtility.SetValue(field.FieldType, value, serializedObject.FindProperty(name));
            serializedObject.ApplyModifiedProperties();
        }

        public bool GetFieldExpand(string name)
        {
            return serializedObject.FindProperty(name).isExpanded;
        }

        public void SetFieldExpand(string name, bool value)
        {
            serializedObject.FindProperty(name).isExpanded = value;
            serializedObject.ApplyModifiedProperties();
        }

        public void Apply()
        {
            foreach (var pair in fields)
            {
                var value = pair.Value.GetValue(this);
                CustomEditorUtility.SetValue(pair.Value.FieldType, value, serializedObject.FindProperty(pair.Key));
            }
            OnApply();
            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void OnApply() { }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= Update;
            serializedObject.Dispose();
        }
    }

    public static class CustomEditorUtility
    {
        public static T GetPrivateField<T>(MonoBehaviour target, string name)
        {
            return (T)GetPrivateField(target.GetType(), name).GetValue(target);
        }

        public static void SetPrivateField<T>(T value, MonoBehaviour target, string name)
        {
            GetPrivateField(target.GetType(), name).SetValue(target, value);
        }

        private static FieldInfo GetPrivateField(Type Type, string name)
        {
            var fields = Type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.Name == name)
                    return field;
            }
            throw new Exception("filed not found");
        }

        public static T GetValue<T>(SerializedProperty property)
        {
            return (T)GetValue(typeof(T), property);
        }

        public static void SetValue<T>(T Data, SerializedProperty property)
        {
            SetValue(typeof(T), Data, property);
        }

        public static void SetValue(Type Type, object data, SerializedProperty property)
        {
            if (Type == typeof(int))
                property.intValue = (int)data;
            else if (Type == typeof(float))
                property.floatValue = (float)data;
            else if (Type == typeof(string))
                property.stringValue = (string)data;
            else if (Type == typeof(bool))
                property.boolValue = (bool)data;
            else if (Type == typeof(double))
                property.doubleValue = (double)data;
            else if (Type == typeof(long))
                property.longValue = (long)data;
            else if (Type == typeof(Vector2))
                property.vector2Value = (Vector2)data;
            else if (Type == typeof(Vector3))
                property.vector3Value = (Vector3)data;
            else if (Type == typeof(Vector4))
                property.vector4Value = (Vector4)data;
            else if (Type == typeof(Color))
                property.colorValue = (Color)data;
            else if (Type == typeof(Quaternion))
                property.quaternionValue = (Quaternion)data;
            else if (Type == typeof(Vector2Int))
                property.vector2IntValue = (Vector2Int)data;
            else if (Type == typeof(Vector3Int))
                property.vector3IntValue = (Vector3Int)data;
            else if (Type == typeof(Bounds))
                property.boundsValue = (Bounds)data;
            else if (Type == typeof(BoundsInt))
                property.boundsIntValue = (BoundsInt)data;
            else if (Type == typeof(Rect))
                property.rectValue = (Rect)data;
            else if (Type == typeof(RectInt))
                property.rectIntValue = (RectInt)data;
            else if (Type == typeof(AnimationCurve))
                property.animationCurveValue = (AnimationCurve)data;
            else if (Type.IsEnum)
                property.intValue = (int)data;
            else if (HaveParent(Type, typeof(Object)))
                property.objectReferenceValue = (Object)data;
            else if (Type.Name == "List`1")
            {
                var elementType = Type.GetGenericArguments()[0];
                PropertyInfo indexer = null;
                foreach (var prop in Type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    if (prop.GetIndexParameters().Length > 0)
                    {
                        indexer = prop;
                        break;
                    }
                var index = new object[1];
                property.arraySize = (int)Type.GetProperty("Count").GetValue(data);
                for (int i = 0; i < property.arraySize; i++)
                {
                    index[0] = i;
                    SetValue(elementType, indexer.GetValue(data, index), property.GetArrayElementAtIndex(i));
                }
            }
            else if (Type.BaseType == typeof(Array))
            {
                var elementType = Type.GetElementType();
                var array = (Array)data;
                property.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++)
                    SetValue(elementType, array.GetValue(i), property.GetArrayElementAtIndex(i));
            }
            else
            {
                var fields = Type.GetFields(BindingFlags.NonPublic | BindingFlags.Public
                    | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var serialize = field.GetCustomAttribute(typeof(SerializeField)) != null;
                    bool nestedArray = false;
                    if (field.FieldType.BaseType == typeof(Array))
                        nestedArray = field.FieldType.GetElementType().BaseType == typeof(Array);
                    if (field.IsPublic || serialize && !nestedArray)
                        SetValue(field.FieldType, field.GetValue(data), property.FindPropertyRelative(field.Name));
                }
            }
        }

        public static object GetValue(Type Type, SerializedProperty property)
        {
            if (Type == typeof(int))
                return property.intValue;
            else if (Type == typeof(float))
                return property.floatValue;
            else if (Type == typeof(string))
                return property.stringValue;
            else if (Type == typeof(bool))
                return property.boolValue;
            else if (Type == typeof(double))
                return property.doubleValue;
            else if (Type == typeof(long))
                return property.longValue;
            else if (Type == typeof(Vector2))
                return property.vector2Value;
            else if (Type == typeof(Vector3))
                return property.vector3Value;
            else if (Type == typeof(Vector4))
                return property.vector4Value;
            else if (Type == typeof(Color))
                return property.colorValue;
            else if (Type == typeof(Quaternion))
                return property.quaternionValue;
            else if (Type == typeof(Vector2Int))
                return property.vector2IntValue;
            else if (Type == typeof(Vector3Int))
                return property.vector3IntValue;
            else if (Type == typeof(Bounds))
                return property.boundsValue;
            else if (Type == typeof(BoundsInt))
                return property.boundsIntValue;
            else if (Type == typeof(Rect))
                return property.rectValue;
            else if (Type == typeof(RectInt))
                return property.rectIntValue;
            else if (Type == typeof(AnimationCurve))
                return property.animationCurveValue;
            else if (Type.IsEnum)
                return property.intValue;
            else if (HaveParent(Type, typeof(Object)))
                return property.objectReferenceValue;
            else if (Type.BaseType == typeof(Array))
            {
                var elementType = Type.GetElementType();
                var array = Array.CreateInstance(elementType, property.arraySize);
                for (int i = 0; i < array.Length; i++)
                    array.SetValue(GetValue(elementType, property.GetArrayElementAtIndex(i)), i);
                return array;
            }
            else if (Type.IsGenericType && Type.Name == "List`1")
            {
                var elementType = Type.GetGenericArguments()[0];
                var array = Activator.CreateInstance(Type);
                var Add = Type.GetMethod("Add");
                for (int i = 0; i < property.arraySize; i++)
                    Add.Invoke(array, new object[] { GetValue(elementType, property.GetArrayElementAtIndex(i)) });
                return array;
            }
            else
            {
                System.Object instance = FormatterServices.GetUninitializedObject(Type);
                var fields = Type.GetFields(BindingFlags.NonPublic | BindingFlags.Public
                    | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var serialize = field.GetCustomAttribute(typeof(SerializeField)) != null;
                    bool nestedArray = false;
                    if (field.FieldType.BaseType == typeof(Array))
                        nestedArray = field.FieldType.GetElementType().BaseType == typeof(Array);
                    if (field.IsPublic || serialize && !nestedArray)
                    {
                        var prop = property.FindPropertyRelative(field.Name);
                        if (prop == null) continue;
                        field.SetValue(instance,
                            GetValue(field.FieldType, prop));
                    }
                }
                return instance;
            }
        }

        private static bool HaveParent(Type target, Type parentType)
        {
            var p = target.BaseType;
            if (p == null)
                return false;
            else if (p == parentType)
                return true;
            else
                return HaveParent(p, parentType);
        }
    }

}