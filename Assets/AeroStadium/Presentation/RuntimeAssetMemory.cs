using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;

namespace AeroStadium.Presentation
{
    /// <summary>Reclaim retired Resources assets in idle batches, without unloading live models.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RuntimeAssetMemory : MonoBehaviour
    {
        const int BatchChanges = 8;
        const float QuietSeconds = 2f, CooldownSeconds = 15f;
        static RuntimeAssetMemory instance;
        int pendingChanges, completedCollections;
        float eligibleAt, lastCompletedAt = -CooldownSeconds;
        bool modelRemoved, memoryPressure, reclaiming;

        public static int CompletedCollections => instance != null ? instance.completedCollections : 0;
        public static bool IsReclaiming => instance != null && instance.reclaiming;

        public static void NotifyModelChanged(bool removed = false)
        {
            if (!Application.isPlaying) return;
            if (instance == null)
                new GameObject("Runtime asset memory").AddComponent<RuntimeAssetMemory>();
            instance.pendingChanges++;
            instance.modelRemoved |= removed;
            instance.eligibleAt = Time.realtimeSinceStartup + QuietSeconds;
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        void OnEnable() => Application.lowMemory += OnLowMemory;
        void OnDisable() => Application.lowMemory -= OnLowMemory;
        void OnDestroy() { if (instance == this) instance = null; }

        void OnLowMemory()
        {
            memoryPressure = true;
            pendingChanges = Mathf.Max(1, pendingChanges);
            eligibleAt = Time.realtimeSinceStartup + .25f;
        }

        void Update()
        {
            if (reclaiming || pendingChanges == 0 || Time.realtimeSinceStartup < eligibleAt) return;
            if (!memoryPressure && !modelRemoved && pendingChanges < BatchChanges) return;
            float cooldown = memoryPressure ? QuietSeconds : CooldownSeconds;
            if (Time.realtimeSinceStartup < lastCompletedAt + cooldown) return;
            StartCoroutine(Reclaim());
        }

        IEnumerator Reclaim()
        {
            reclaiming = true;
            // Destroy(model) finishes at the end of its frame. Do not inspect the asset graph earlier.
            yield return null;
            int changes = pendingChanges;
            string reason = memoryPressure ? "memory-pressure" : modelRemoved ? "models-cleared" : "model-batch";
            pendingChanges = 0; modelRemoved = false; memoryPressure = false;
            long before = Profiler.GetTotalAllocatedMemoryLong();
            float startedAt = Time.realtimeSinceStartup;
            // Unity follows scene objects, component fields and static references. No live asset is targeted.
            yield return Resources.UnloadUnusedAssets();
            long after = Profiler.GetTotalAllocatedMemoryLong();
            completedCollections++;
            lastCompletedAt = Time.realtimeSinceStartup;
            reclaiming = false;
            Debug.Log("[asset-memory] cleanup=" + completedCollections + " reason=" + reason + " changes=" + changes
                + " unityAllocatedBeforeBytes=" + before + " unityAllocatedAfterBytes=" + after
                + " reclaimedBytes=" + System.Math.Max(0L, before - after)
                + " seconds=" + (lastCompletedAt - startedAt).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
