using System;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Apply imported expression controls after the native skeleton has been animated.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class NativeMaterialBridge : MonoBehaviour
    {
        [Serializable] public sealed class UvControl
        {
            public Transform bone;
            public Renderer renderer;
            public int materialIndex, propertyType;
            public string propertyName;
            public Vector2 baseScale = Vector2.one;
            public bool smokeMask;
        }
        [Serializable] public sealed class VisibilityControl
        {
            public Transform bone;
            public Renderer renderer;
        }
        [SerializeField] UvControl[] uv = Array.Empty<UvControl>();
        [SerializeField] VisibilityControl[] visibility = Array.Empty<VisibilityControl>();
        MaterialPropertyBlock block;

        public void Configure(UvControl[] controls, VisibilityControl[] visible)
        {
            uv = controls ?? Array.Empty<UvControl>();
            visibility = visible ?? Array.Empty<VisibilityControl>();
            Apply();
        }
        void LateUpdate() => Apply();

        public static Vector4 EvaluateOffset(Vector3 position, Vector3 scale, int type, Vector2 baseScale)
        {
            float x = position.x * 100f, y = -position.y * 100f;
            Vector2 s = baseScale;
            if (type == 4 || type == 7) s = new Vector2(scale.x, scale.y);
            if (type == 2 || type == 3)
            {
                x = Mathf.Round(x * 2f) * .5f;
                y = Mathf.Round(y * 4f) * .25f;
            }
            if (type == 5) y = x;
            if (type == 6) return new Vector4(s.x, s.y, x, y);
            return new Vector4(s.x, s.y, x * s.x, y * s.y);
        }

        // CustomNodeProperty.UpdatePropertyBlockSmokeMask wraps only U and does
        // not quantize the eye-atlas steps used by the ordinary material path.
        public static Vector4 EvaluateSmokeOffset(Vector3 position, Vector3 scale, int type, Vector2 baseScale)
        {
            Vector2 s = type == 4 || type == 7 ? new Vector2(scale.x, scale.y) : baseScale;
            return new Vector4(s.x, s.y, Mathf.Repeat(position.x * 100f, 1f) * s.x, -position.y * 100f * s.y);
        }

        public void Apply()
        {
            block ??= new MaterialPropertyBlock();
            foreach (var entry in uv)
            {
                if (entry == null || entry.bone == null || entry.renderer == null) continue;
                entry.renderer.GetPropertyBlock(block, entry.materialIndex);
                Vector3 p = entry.bone.localPosition;
                if (entry.propertyType == 0)
                    block.SetFloat("_" + entry.propertyName, -100f * p.x);
                else if (entry.propertyType == 1)
                {
                    if (entry.smokeMask) block.SetFloat("_Mask0UVTranslateU", -100f * p.x);
                    else
                    {
                        block.SetFloat("_" + entry.propertyName + "U", -100f * p.x);
                        block.SetFloat("_" + entry.propertyName + "V", 100f * p.y);
                    }
                }
                else
                {
                    string property = entry.propertyType == 3 || entry.propertyType == 7 || entry.propertyType == 8
                        ? "_LayerMap_ST" : entry.propertyType == 6 ? "_GroundEffectMaskTex_ST" : "_BaseMap_ST";
                    block.SetVector(property, entry.smokeMask
                        ? EvaluateSmokeOffset(p, entry.bone.localScale, entry.propertyType, entry.baseScale)
                        : EvaluateOffset(p, entry.bone.localScale, entry.propertyType, entry.baseScale));
                }
                entry.renderer.SetPropertyBlock(block, entry.materialIndex);
                block.Clear();
            }
            foreach (var entry in visibility)
                if (entry != null && entry.bone != null && entry.renderer != null)
                    entry.renderer.enabled = Mathf.Abs(entry.bone.localPosition.x) >= .005f;
        }
    }
}
