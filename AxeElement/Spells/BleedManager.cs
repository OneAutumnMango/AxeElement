using System.Collections.Generic;
using UnityEngine;

namespace AxeElement
{
    public static class BleedManager
    {
        private static readonly Dictionary<int, float> expiryTimes = new Dictionary<int, float>();
        private static readonly Dictionary<int, BleedEffect> effects = new Dictionary<int, BleedEffect>();

        private static readonly Color BleedColor = new Color(0.15f, 0.02f, 0.01f);
        private static readonly Color BleedLightColor = new Color(0.20f, 0.04f, 0.02f);
        public const float BLEED_DURATION_PUBLIC = 5f;
        private const float BLEED_DURATION = BLEED_DURATION_PUBLIC;

        public static void ApplyBleed(int targetOwner, GameObject target, UnityEngine.Object prefab)
        {
            if (effects.TryGetValue(targetOwner, out var existing) && existing != null)
            {
                // Already bleeding — refresh timer only
                expiryTimes[targetOwner] = Time.time + BLEED_DURATION;
                existing.RefreshTimer();
                return;
            }

            // New bleed
            expiryTimes[targetOwner] = Time.time + BLEED_DURATION;

            if (prefab != null && target != null)
            {
                try
                {
                    if (UnityEngine.Object.Instantiate(prefab, target.transform.position + Vector3.up * 0.9f, Quaternion.identity) is GameObject go)
                    {
                        go.transform.SetParent(target.transform);
                        RecolorDark(go);
                        var fx = go.AddComponent<BleedEffect>();
                        fx.targetOwnerId = targetOwner;
                        effects[targetOwner] = fx;
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning($"[Bleed] Effect instantiation failed: {ex.Message}");
                }
            }
        }

        public static bool IsBleedActive(int targetOwner)
        {
            return expiryTimes.TryGetValue(targetOwner, out float exp) && Time.time < exp;
        }

        public static void RefreshBleed(int targetOwner)
        {
            if (expiryTimes.ContainsKey(targetOwner))
            {
                expiryTimes[targetOwner] = Time.time + BLEED_DURATION;
                if (effects.TryGetValue(targetOwner, out var fx) && fx != null)
                    fx.RefreshTimer();
            }
        }

        public static void OnEffectExpired(int targetOwner)
        {
            expiryTimes.Remove(targetOwner);
            effects.Remove(targetOwner);
        }

        private static void RecolorDark(GameObject fx)
        {
            foreach (Renderer r in fx.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Color"))
                        mat.color = BleedColor;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", BleedColor * 2f);
                }
            }
            foreach (Light lt in fx.GetComponentsInChildren<Light>(true))
                lt.color = BleedLightColor;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = BleedColor;
            }
        }
    }

    public class BleedEffect : MonoBehaviour
    {
        public int targetOwnerId;

        private void Start()
        {
            Invoke("SelfDestruct", BleedManager.BLEED_DURATION_PUBLIC);
        }

        public void RefreshTimer()
        {
            CancelInvoke("SelfDestruct");
            Invoke("SelfDestruct", BleedManager.BLEED_DURATION_PUBLIC);
        }

        private void SelfDestruct()
        {
            BleedManager.OnEffectExpired(targetOwnerId);
            Destroy(gameObject);
        }
    }
}
