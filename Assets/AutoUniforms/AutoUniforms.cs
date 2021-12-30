using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

namespace AutoUniforms
{
    public class UniformBuffer
    {
        struct FieldHolder
        {
            public FieldInfo fieldInfo;
            public int uniformId;

            public FieldHolder(FieldInfo fieldInfo, int uniformId)
            {
                this.fieldInfo = fieldInfo;
                this.uniformId = uniformId;
            }
        }
        struct PropertyHolder
        {
            public PropertyInfo propertyInfo;
            public int uniformId;

            public PropertyHolder(PropertyInfo propertyInfo, int uniformId)
            {
                this.propertyInfo = propertyInfo;
                this.uniformId = uniformId;
            }
        }

        private bool isInit = false;
        private FieldHolder[] fields;
        private PropertyHolder[] properties;

        //upload targets
        List<ComputeShader> computeShaders = new List<ComputeShader>();

        public void AddUploadTarget_ComputeShader(ComputeShader computeShader)
        {
            computeShaders.Add(computeShader);
        }

        public void Init()
        {
            Assert.IsFalse(isInit);

            Type bufferClassType = this.GetType();//most specific type of the instance (likely the class that derives from this class)
            var outputAttr = (ShaderCodeOutputAttribute)bufferClassType.GetCustomAttribute(typeof(ShaderCodeOutputAttribute));

            FieldInfo[] fieldInfos;
            PropertyInfo[] propInfos;
            GetFieldsAndProperties(bufferClassType, out fieldInfos, out propInfos);

            fields = new FieldHolder[fieldInfos.Length];
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                string uniformName = outputAttr.prefix + fieldInfos[i].Name;
                fields[i] = new FieldHolder(fieldInfos[i], Shader.PropertyToID(uniformName));
            }

            properties = new PropertyHolder[propInfos.Length];
            for (int i = 0; i < propInfos.Length; i++)
            {
                string uniformName = outputAttr.prefix + propInfos[i].Name;
                properties[i] = new PropertyHolder(propInfos[i], Shader.PropertyToID(uniformName));
            }

            isInit = true;
        }

        public void UploadAll()
        {
            foreach (var shader in computeShaders)
                UploadToComputeShader(shader);
        }

        private void UploadToComputeShader(ComputeShader shader)
        {
            Assert.IsTrue(isInit);

            foreach (var field in fields)
                SetUniform_ComputeShader(shader, field.uniformId, field.fieldInfo.GetValue(this));

            foreach (var property in properties)
                SetUniform_ComputeShader(shader, property.uniformId, property.propertyInfo.GetValue(this));
        }

        private static void SetUniform_ComputeShader(ComputeShader shader, int uniformId, dynamic value)
        {
            if      (value.GetType() == typeof(int)) shader.SetInt(uniformId, value);
            else if (value.GetType() == typeof(uint)) shader.SetInt(uniformId, value);
            else if (value.GetType() == typeof(float)) shader.SetFloat(uniformId, value);
            else if (value.GetType() == typeof(bool)) shader.SetBool(uniformId, value);
            else if (value.GetType() == typeof(Vector2)) shader.SetFloats(uniformId, new float[] { value.x, value.y });
            else if (value.GetType() == typeof(Vector3)) shader.SetFloats(uniformId, new float[] { value.x, value.y, value.z });
            else if (value.GetType() == typeof(Vector4)) shader.SetFloats(uniformId, new float[] { value.x, value.y, value.z, value.w });
            else if (value.GetType() == typeof(Vector2Int)) shader.SetInts(uniformId, new int[] { value.x, value.y });
            else if (value.GetType() == typeof(Vector3Int)) shader.SetInts(uniformId, new int[] { value.x, value.y, value.z });
        }

        private static void GetFieldsAndProperties(Type bufferClassType, out FieldInfo[] fields, out PropertyInfo[] properties)
        {
            //get fields
            fields = bufferClassType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fields = fields.Where(f => !Attribute.IsDefined(f, typeof(DontUploadAttribute))).ToArray();

            //get properties
            properties = bufferClassType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            properties = properties.Where(p => !Attribute.IsDefined(p, typeof(DontUploadAttribute))).ToArray();
        }

        #region shader code generation

        //key: C# type. value: hlsl type as a string
        private static readonly Dictionary<Type, string> typeDict = new Dictionary<Type, string>()
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

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptReload()
        {
            //generate shader code for all classes that derive from UniformBuffer
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(UniformBuffer)))
                        GenerateShaderCode(type);
                }
        }

        private static void GenerateShaderCode(Type bufferClassType)
        {
            string shaderCode = "//contents of this file are procedurally generated by AutoUniforms\n";

            var outputAttr = (ShaderCodeOutputAttribute)bufferClassType.GetCustomAttribute(typeof(ShaderCodeOutputAttribute));
            Assert.IsNotNull(outputAttr);
            outputAttr.FillDefaultValues(bufferClassType);

            //get fields and properties
            FieldInfo[] fields;
            PropertyInfo[] properties;
            GetFieldsAndProperties(bufferClassType, out fields, out properties);

            //insert fields and properties into code
            foreach (FieldInfo field in fields)
                shaderCode += $"\n{typeDict[field.FieldType]} {outputAttr.prefix}{field.Name};";
            foreach (PropertyInfo property in properties)
                shaderCode += $"\n{typeDict[property.PropertyType]} {outputAttr.prefix}{property.Name};";

            File.WriteAllText(outputAttr.filePath, shaderCode);
        }
        #endregion
    }

    #region attributes
    //make AutoUniforms ignore a field or a property
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DontUploadAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ShaderCodeOutputAttribute : Attribute
    {
        public string filePath;
        public string prefix = "";

        public ShaderCodeOutputAttribute(string filePath)
        {
            this.filePath = filePath;
        }

        public void FillDefaultValues(Type classType)
        {
            if (prefix == "")
                prefix = classType.Name + "_";
        }
    }
    #endregion
}