using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Restore source smoke vertex controls without rewriting the native GLB or skeleton.</summary>
    public static class NativeEffectGeometry
    {
        [Serializable] sealed class Document { public MeshEntry[] meshes; public Accessor[] accessors; public BufferView[] bufferViews; }
        [Serializable] sealed class MeshEntry { public string name; public Primitive[] primitives; }
        [Serializable] sealed class Primitive { public Attributes attributes; }
        [Serializable] sealed class Attributes { public int POSITION; public int _NATIVE_VERTEX_MASKS; }
        [Serializable] sealed class Accessor { public int bufferView, byteOffset, componentType, count; public string type; }
        [Serializable] sealed class BufferView { public int buffer, byteOffset, byteLength, byteStride; }
        [Serializable] sealed class Report { public string sourceGLB, implementation, limitation; public MeshReport[] meshes; }
        [Serializable] sealed class MeshReport { public string renderer, savedMesh; public int vertices, billboardVertices; public float largestEncodedRadius; }

        public static void Restore(GameObject model, string folder, string glbPath)
        {
            Renderer[] smoke = model.GetComponentsInChildren<Renderer>(true).Where(renderer =>
                renderer.sharedMaterials.Any(material => material != null && material.HasProperty("_NativeSmokeBillboard")
                    && material.GetFloat("_NativeSmokeBillboard") > .5f)).ToArray();
            if (smoke.Length == 0) return;

            byte[] bytes = File.ReadAllBytes(glbPath);
            if (bytes.Length < 28 || BitConverter.ToUInt32(bytes, 0) != 0x46546c67
                || BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length
                || BitConverter.ToUInt32(bytes, 16) != 0x4e4f534a)
                throw new InvalidDataException("GLB natif invalide pour les sommets de fumée.");
            int jsonLength = checked((int)BitConverter.ToUInt32(bytes, 12));
            int binaryHeader = checked(20 + jsonLength);
            if (jsonLength < 2 || binaryHeader > bytes.Length - 8
                || BitConverter.ToUInt32(bytes, binaryHeader + 4) != 0x004e4942)
                throw new InvalidDataException("Bloc binaire GLB natif absent.");
            int binaryStart = binaryHeader + 8;
            int binaryLength = checked((int)BitConverter.ToUInt32(bytes, binaryHeader));
            if (binaryLength < 0 || binaryLength > bytes.Length - binaryStart)
                throw new InvalidDataException("Bloc binaire GLB natif tronqué.");
            Document doc = JsonUtility.FromJson<Document>(Encoding.UTF8.GetString(bytes, 20, jsonLength));
            if (doc?.meshes == null || doc.accessors == null || doc.bufferViews == null)
                throw new InvalidDataException("Attributs GLB natifs absents.");

            string outputFolder = folder + "/NativeEffectMeshes";
            if (!AssetDatabase.IsValidFolder(outputFolder)) AssetDatabase.CreateFolder(folder, "NativeEffectMeshes");
            var reports = new List<MeshReport>();
            for (int index = 0; index < smoke.Length; index++)
            {
                Renderer renderer = smoke[index];
                MeshEntry source = doc.meshes.SingleOrDefault(mesh => mesh.name == renderer.name);
                if (source?.primitives == null || source.primitives.Length != 1 || source.primitives[0].attributes == null)
                    throw new InvalidDataException("Maillage de fumée natif ambigu : " + renderer.name);
                Primitive primitive = source.primitives[0];
                // All supplied SmokeMask_MT meshes use one primitive and a float4
                // custom attribute. A count or position mismatch must fail visibly.
                Vector4[] masks = ReadFloatAttribute(primitive.attributes._NATIVE_VERTEX_MASKS, "VEC4", 4);
                Vector4[] positions = ReadFloatAttribute(primitive.attributes.POSITION, "VEC3", 3);
                Mesh input = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (input == null || !input.isReadable || input.vertexCount != masks.Length || masks.Length != positions.Length)
                    throw new InvalidDataException("Nombre de contrôles de fumée incohérent : " + renderer.name);
                Vector3[] vertices = input.vertices;
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    Vector4 p = positions[vertex];
                    // glTFast reverses glTF's X reflection when importing into Unity.
                    if ((vertices[vertex] - new Vector3(-p.x, p.y, p.z)).sqrMagnitude > 1e-8f)
                        throw new InvalidDataException("Ordre de sommets natif modifié : " + renderer.name);
                    Vector4 m = masks[vertex];
                    if (m.x < 0 || m.x > 1 || m.y < 0 || m.y > 1 || m.z < 0 || m.z > 1)
                        throw new InvalidDataException("Contrôle de fumée natif invalide : " + renderer.name);
                }
                string path = outputFolder + "/" + index.ToString("000") + ".asset";
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { saved = UnityEngine.Object.Instantiate(input); AssetDatabase.CreateAsset(saved, path); }
                else EditorUtility.CopySerialized(input, saved);
                saved.name = input.name + "_NativeEffectControls";
                // Preserve these data values separately from diffuse COLOR and the
                // original UV0/UV1; glTFast ignores the custom glTF attribute.
                saved.SetUVs(4, new List<Vector4>(masks));
                float radius = masks.Max(mask => mask.z);
                if (renderer is SkinnedMeshRenderer target)
                {
                    target.sharedMesh = saved;
                    Bounds bounds = target.localBounds;
                    bounds.Expand(2f * radius);
                    target.localBounds = bounds;
                }
                else renderer.GetComponent<MeshFilter>().sharedMesh = saved;
                EditorUtility.SetDirty(saved);
                reports.Add(new MeshReport { renderer = renderer.name, savedMesh = path, vertices = masks.Length,
                    billboardVertices = masks.Count(mask => Mathf.Abs(mask.x - .5f) > .49f || Mathf.Abs(mask.y - .5f) > .49f),
                    largestEncodedRadius = radius });
            }
            NativeSmokeBillboardScale scaleDriver = model.GetComponent<NativeSmokeBillboardScale>();
            if (scaleDriver == null) scaleDriver = model.AddComponent<NativeSmokeBillboardScale>();
            scaleDriver.Configure(model.transform);
            File.WriteAllText(folder + "/native-smoke-geometry-report.json", JsonUtility.ToJson(new Report {
                sourceGLB = glbPath,
                implementation = "Source COLOR values restored in TEXCOORD4; camera-plane expansion uses RG corner and B size; _BillboardScale follows native model lossyScale.x",
                limitation = "Vertex-control semantics are source-grounded; exact NVN vertex arithmetic and B-to-radius calibration remain an adaptation",
                meshes = reports.ToArray() }, true));

            Vector4[] ReadFloatAttribute(int accessorIndex, string expectedType, int components)
            {
                if (accessorIndex < 0 || accessorIndex >= doc.accessors.Length)
                    throw new InvalidDataException("Accesseur de fumée absent.");
                Accessor accessor = doc.accessors[accessorIndex];
                if (accessor.componentType != 5126 || accessor.type != expectedType || accessor.count <= 0
                    || accessor.bufferView < 0 || accessor.bufferView >= doc.bufferViews.Length || accessor.byteOffset < 0)
                    throw new InvalidDataException("Accesseur de fumée incompatible.");
                BufferView view = doc.bufferViews[accessor.bufferView];
                int elementBytes = components * 4;
                int stride = view.byteStride == 0 ? elementBytes : view.byteStride;
                long required = (long)accessor.byteOffset + (long)(accessor.count - 1) * stride + elementBytes;
                if (view.buffer != 0 || view.byteOffset < 0 || view.byteLength < 0 || stride < elementBytes
                    || required > view.byteLength || (long)view.byteOffset + view.byteLength > binaryLength)
                    throw new InvalidDataException("Attribut de fumée hors du bloc binaire.");
                var values = new Vector4[accessor.count];
                int start = checked(binaryStart + view.byteOffset + accessor.byteOffset);
                for (int vertex = 0; vertex < values.Length; vertex++)
                {
                    int offset = checked(start + vertex * stride);
                    Vector4 value = Vector4.zero;
                    for (int c = 0; c < components; c++)
                    {
                        float number = BitConverter.ToSingle(bytes, offset + c * 4);
                        if (float.IsNaN(number) || float.IsInfinity(number))
                            throw new InvalidDataException("Attribut de fumée non fini.");
                        value[c] = number;
                    }
                    values[vertex] = value;
                }
                return values;
            }
        }
    }
}
