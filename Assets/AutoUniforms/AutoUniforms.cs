using System;
using UnityEngine;
using UnityEngine.Assertions;

using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace AutoUniforms
{
    public class _UniformBuffer
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
                    if (type.IsSubclassOf(typeof(_UniformBuffer)))
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

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class OutputFilePathAttribute : Attribute
    {
        public string path;

        public OutputFilePathAttribute(string path)
        {
            this.path = path;
        }
    }
    #endregion

    public abstract class Uniform<T> 
    {
        public T value;
        public readonly string shaderUniformName;
        public readonly string outputFilePath;
        public int shaderPropId { get; private set; }
        public bool isInit { get; private set; } = false;
        public Targets targets { get; private set; } = new Targets();

        public Uniform(T value, string shaderUniformName, string outputFilePath)
        {
            this.value = value;
            this.shaderUniformName = shaderUniformName;
            this.outputFilePath = outputFilePath;
        }

        public void AddUploadTarget(ComputeShader shader) =>
            targets.computeShaders.Add(shader);
        public void AddUploadTarget(Material material) =>
            targets.materials.Add(material);

        public void Init()
        {
            Assert.IsFalse(isInit);

            shaderPropId = Shader.PropertyToID(shaderUniformName);

            isInit = true;
        }
    }

    public abstract class UniformGroup
    {
        public readonly string name;

        private bool isInit = false;
        private Targets targets = new Targets();

        public UniformGroup(string name)
        {
            this.name = name;
        }

        public void Init()
        {
            Assert.IsFalse(isInit);

            isInit = true;
        }
    }

    public class ConstUniformGroupContainer<T>
    {
        public readonly string name;
        public readonly string outputFilePath;

        private T[] uniformGroups;
        private int lastAddedIndex = 0;
        private ComputeBuffer computeBuffer;

        public ConstUniformGroupContainer(int capacity, int uniformGroupSize, string name, string outputFilePath)
        {
            this.name = name;
            this.outputFilePath = outputFilePath;

            uniformGroups = new T[capacity];
            computeBuffer = new ComputeBuffer(capacity, uniformGroupSize, ComputeBufferType.Constant, ComputeBufferMode.Immutable);
        }

        public Type GetGroupType() =>
            uniformGroups.GetType().GetElementType();

        public void Release() =>
            computeBuffer.Release();

        ~ConstUniformGroupContainer() =>
            Release();

        public void AddUniformGroup(T group) =>
            uniformGroups[lastAddedIndex++] = group;

        public void UploadTo(ComputeShader shader)
        {
            shader.SetConstantBuffer(name, computeBuffer, 0, computeBuffer.count * computeBuffer.stride);
        }
    }

    public class Targets
    {
        public List<ComputeShader> computeShaders = new List<ComputeShader>();
        public List<Material> materials = new List<Material>();
    }

    #region classes derived from Uniform
    [System.Serializable]
    public class Uniform_Int : Uniform<int> {
        public Uniform_Int(int value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_UInt : Uniform<uint> {
        public Uniform_UInt(uint value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Float : Uniform<float> {
        public Uniform_Float(float value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Bool : Uniform<bool> {
        public Uniform_Bool(bool value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Vector2 : Uniform<Vector2> {
        public Uniform_Vector2(Vector2 value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Vector3 : Uniform<Vector3> {
        public Uniform_Vector3(Vector3 value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Vector4 : Uniform<Vector4> {
        public Uniform_Vector4(Vector4 value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Vector2Int : Uniform<Vector2Int> {
        public Uniform_Vector2Int(Vector2Int value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }

    [System.Serializable]
    public class Uniform_Vector3Int : Uniform<Vector3Int> {
        public Uniform_Vector3Int(Vector3Int value, string shaderVarName, string outputFilePath) : base(value, shaderVarName, outputFilePath) { }
    }
    #endregion
}

public static class UploadExtensions
{
    private static class LowLevel
    {
        public static void Upload(ComputeShader dest, int id, int value) => dest.SetInt(id, value);
        public static void Upload(ComputeShader dest, int id, uint value) => dest.SetInt(id, (int)value);
        public static void Upload(ComputeShader dest, int id, float value) => dest.SetFloat(id, value);
        public static void Upload(ComputeShader dest, int id, bool value) => dest.SetBool(id, value);
        public static void Upload(ComputeShader dest, int id, Vector2 value) => dest.SetFloats(id, value.x, value.y );
        public static void Upload(ComputeShader dest, int id, Vector3 value) => dest.SetFloats(id, value.x, value.y, value.z );
        public static void Upload(ComputeShader dest, int id, Vector4 value) => dest.SetFloats(id, value.x, value.y, value.z, value.w );
        public static void Upload(ComputeShader dest, int id, Vector2Int value) => dest.SetInts(id, value.x, value.y);
        public static void Upload(ComputeShader dest, int id, Vector3Int value) => dest.SetInts(id, value.x, value.y, value.z);
        public static void Upload<OtherType>(ComputeShader dest, int id, OtherType value) => FailUpload(typeof(OtherType));


        public static void Upload(Material dest, int id, int value) => dest.SetInt(id, value);
        public static void Upload(Material dest, int id, uint value) => dest.SetInt(id, (int)value);
        public static void Upload(Material dest, int id, float value) => dest.SetFloat(id, value);
        public static void Upload(Material dest, int id, Vector2 value) => dest.SetVector(id, value);
        public static void Upload(Material dest, int id, Vector3 value) => dest.SetVector(id, value);
        public static void Upload(Material dest, int id, Vector4 value) => dest.SetVector(id, value);
        public static void Upload<OtherType>(Material dest, int id, OtherType value) => FailUpload(typeof(OtherType));


        public static void FailUpload(Type type) =>
            throw new Exception($"Cannot upload type {type}.");
    }

    #region UploadToAll repeated many times with different type
    public static void UploadToAll(this AutoUniforms.Uniform<int> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<uint> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<float> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<bool> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<Vector2> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<Vector3> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<Vector4> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<Vector2Int> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }

    public static void UploadToAll(this AutoUniforms.Uniform<Vector3Int> uniform)
    {
        Assert.IsTrue(uniform.isInit);
        foreach (var shader in uniform.targets.computeShaders)
            LowLevel.Upload(shader, uniform.shaderPropId, uniform.value);
        foreach (var material in uniform.targets.materials)
            LowLevel.Upload(material, uniform.shaderPropId, uniform.value);
    }
    #endregion
}