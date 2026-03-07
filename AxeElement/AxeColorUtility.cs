using UnityEngine;

namespace AxeElement
{
    public static class AxeColorUtility
    {
        public static readonly Color CrimsonColor = new Color(0.40f, 0.02f, 0.02f);

        /// <summary>
        /// Applies the crimson red color to all renderers, lights, and particle systems
        /// in the given GameObject and its children.
        /// </summary>
        public static void ApplyCrimsonColor(GameObject gameObject)
        {
            if (gameObject == null) return;

            // Apply to all renderers
            foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Color"))
                            mat.color = CrimsonColor;
                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", CrimsonColor * 2f);
                    }
                }
            }

            // Apply to all lights
            foreach (Light light in gameObject.GetComponentsInChildren<Light>(true))
            {
                light.color = CrimsonColor;
            }

            // Apply to all particle systems
            foreach (ParticleSystem ps in gameObject.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = CrimsonColor;
            }
        }
    }
}
