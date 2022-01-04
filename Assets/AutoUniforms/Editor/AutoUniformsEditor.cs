using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace AutoUniforms
{
    namespace Editor
    {
        [CustomPropertyDrawer(typeof(Uniform<>), true)]
        public class UniformDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.PropertyField(position, property.FindPropertyRelative("value"), label, true);
                EditorGUI.EndProperty();
            }
        }

        public static class CodeGeneration
        {
            [UnityEditor.Callbacks.DidReloadScripts]
            private static void OnScriptReload()
            {
                GenerateCodeForAllUniforms();
            }

            private static void GenerateCodeForAllUniforms()
            {
                //key: file path to future source code file
                //value: future contents of the source code file
                var shaderCodeBuffer = new Dictionary<string, string>();

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                    foreach (var parentType in assembly.GetTypes())
                    {
                        foreach (var uniform in GetObjectsOfType(typeof(Uniform<>), parentType))
                            AddCodeForUniform(uniform, shaderCodeBuffer);

                        foreach (var groupContainer in GetObjectsOfType(typeof(ConstUniformGroupContainer<>), parentType))
                            AddCodeForConstUniformGroupContainer(groupContainer, shaderCodeBuffer);
                    }

                WriteCodeToFiles(shaderCodeBuffer);
            }

            private static List<dynamic> GetObjectsOfType(Type objectType, Type parentType)
            {
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var fields = parentType.GetFields(bindingFlags).Where(field => IsSubclassOfRawGeneric(field.FieldType, objectType));

                var objects = new List<dynamic>();

                if (fields.Count() > 0)
                {
                    var parentInstance = Activator.CreateInstance(parentType);//instance of the fields' parent class
                    foreach (var field in fields)
                    {
                        object obj = field.GetValue(parentInstance);
                        objects.Add(Cast(obj, field.FieldType));
                    }
                }

                return objects;
            }

            //key: C# type. value: hlsl type as a string
            private static readonly Dictionary<Type, string> typeTable = new Dictionary<Type, string>()
            {
                {typeof(int), "int"},
                {typeof(uint), "uint"},
                {typeof(float), "float"},
                {typeof(bool), "bool"},
                {typeof(Vector2), "float2"},
                {typeof(Vector3), "float3"},
                {typeof(Vector4), "float4"},
                {typeof(Vector2Int), "int2"},
                {typeof(Vector3Int), "int3"}
            };

            private static void AddCodeForUniform<T>(Uniform<T> uniform, Dictionary<string, string> shadeCodeBuffer)
            {
                string filePath = uniform.outputFilePath;
                StartCodeIfNotStarted(filePath, shadeCodeBuffer);
                shadeCodeBuffer[filePath] += $"\n{typeTable[typeof(T)]} {uniform.shaderUniformName };";
            }

            private static void AddCodeForConstUniformGroupContainer<T>(ConstUniformGroupContainer<T> groupContainer, Dictionary<string, string> shadeCodeBuffer)
            {
                string filePath = groupContainer.outputFilePath;
                StartCodeIfNotStarted(filePath, shadeCodeBuffer);
                Type groupType = groupContainer.GetGroupType();

                //representation of the uniform group type in shade code
                shadeCodeBuffer[filePath] += $"struct {groupType.Name}\n{{";
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var field in groupType.GetFields(bindingFlags))
                {
                    shadeCodeBuffer[filePath] += $"    {typeTable[field.FieldType]} {field.Name};";
                }
                shadeCodeBuffer[filePath] += $"}}\n";

                //structured buffer
                shadeCodeBuffer[filePath] += $"\nStructuredBuffer<{groupType.Name}> {groupContainer.name};";
            }

            private static void StartCodeIfNotStarted(string filePath, Dictionary<string, string> shadeCodeBuffer)
            {
                if (!shadeCodeBuffer.ContainsKey(filePath))
                    shadeCodeBuffer[filePath] = "//contents of this file have been generated by AutoUniforms\n";
            }

            private static void WriteCodeToFiles(Dictionary<string, string> shaderCodeBuffer)
            {
                foreach (KeyValuePair<string, string> path_code_pair in shaderCodeBuffer)
                {
                    System.IO.File.WriteAllText(path_code_pair.Key, path_code_pair.Value);
                }
            }

            private static bool IsSubclassOfRawGeneric(Type potentialSubClass, Type generic)
            {
                while (potentialSubClass != null && potentialSubClass != typeof(object))
                {
                    var cur = potentialSubClass.IsGenericType ? potentialSubClass.GetGenericTypeDefinition() : potentialSubClass;
                    if (generic == cur)
                    {
                        return true;
                    }
                    potentialSubClass = potentialSubClass.BaseType;
                }
                return false;
            }

            public static dynamic Cast(dynamic obj, Type castTo)
            {
                return Convert.ChangeType(obj, castTo);
            }
        }
    }
}