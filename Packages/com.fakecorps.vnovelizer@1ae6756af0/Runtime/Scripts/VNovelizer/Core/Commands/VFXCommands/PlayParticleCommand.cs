using System;
using System.Collections;
using UnityEngine;
using VNovelizer.Core.API;

namespace VNovelizer.Core.Commands
{
    public class PlayParticleCommand : VNCommand
    {
        private const float MaxWaitSeconds = 1f;

        public override string CommandName { get { return "playparticle"; } }

        public override bool Execute(string args)
        {
            if (string.IsNullOrEmpty(args)) return false;
            return Play(args.Trim(), null);
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            if (string.IsNullOrEmpty(args)) yield break;

            bool completed = false;
            Play(args.Trim(), _ => completed = true);

            float startedAt = Time.realtimeSinceStartup;
            while (!completed && Time.realtimeSinceStartup - startedAt < MaxWaitSeconds)
            {
                yield return null;
            }
        }

        private bool Play(string effectName, Action<bool> onCompleted)
        {
            if (string.IsNullOrEmpty(effectName)) return false;

            VNAPI.RegisterEffect(effectName);

            Transform parent = VNAPI.GetEffectLayer();
            if (parent == null)
            {
                onCompleted?.Invoke(false);
                return false;
            }

            string objName = "VNEffect_" + effectName;
            if (parent.Find(objName) != null)
            {
                onCompleted?.Invoke(true);
                return true;
            }

            string path = VNProjectConfig.Instance.ParticalEffectPath + "/" + effectName;
            PoolManager.GetInstance().GetObj(path, go =>
            {
                bool success = ApplyEffectObject(effectName, path, objName, go);
                onCompleted?.Invoke(success);
            });

            return true;
        }

        private bool ApplyEffectObject(string effectName, string path, string objName, GameObject go)
        {
            if (go == null)
            {
                Debug.LogError($"[PlayParticle] Effect not found: {path}");
                return false;
            }

            go.name = objName;
            Transform currentParent = VNAPI.GetEffectLayer();
            if (currentParent == null
                || !VNAPI.GetActiveEffectNames().Contains(effectName)
                || currentParent.Find(objName) != null)
            {
                PoolManager.GetInstance().PushObj(path, go);
                return false;
            }

            go.transform.SetParent(currentParent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            ParticleSystem particleSystem = go.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = true;
                particleSystem.Play();
            }

            Coffee.UIExtensions.UIParticle uiParticle = go.GetComponent<Coffee.UIExtensions.UIParticle>();
            if (uiParticle != null)
            {
                uiParticle.Play();
            }

            return true;
        }

        public override void Simulate(string args)
        {
            if (!string.IsNullOrEmpty(args))
            {
                VNAPI.RegisterEffect(args.Trim());
            }
        }
    }
}
