using System.Collections.Generic;
using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>
    /// Small runtime-only celebration effect: three particle bursts and a
    /// world-space HappyBirthday title. It intentionally owns no game state.
    /// </summary>
    public sealed class CelebrationFireworks : MonoBehaviour
    {
        struct Burst
        {
            public ParticleSystem System;
            public float StartAt;
            public bool Emitted;
        }

        readonly List<Burst> bursts = new List<Burst>();
        TextMesh birthdayText;
        float elapsed;

        public static CelebrationFireworks Create(Vector3 focus)
        {
            var root = new GameObject("庆典烟花");
            var fireworks = root.AddComponent<CelebrationFireworks>();
            fireworks.Build(focus);
            return fireworks;
        }

        void Build(Vector3 focus)
        {
            var titleObject = new GameObject("HappyBirthday");
            titleObject.transform.SetParent(transform, false);
            titleObject.transform.position = focus + Vector3.up * 5.2f + Vector3.back * .2f;
            titleObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            birthdayText = titleObject.AddComponent<TextMesh>();
            birthdayText.text = "Happy\nBirthday";
            birthdayText.anchor = TextAnchor.MiddleCenter;
            birthdayText.alignment = TextAlignment.Center;
            birthdayText.fontSize = 64;
            birthdayText.characterSize = .12f;
            birthdayText.fontStyle = FontStyle.Bold;
            birthdayText.color = new Color(1f, .86f, .25f, 0f);
            birthdayText.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI", 64);
            if (birthdayText.font == null)
                birthdayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            AddBurst(focus + new Vector3(-5f, 4.2f, .8f), new Color(1f, .24f, .48f), 0f);
            AddBurst(focus + new Vector3(0f, 5.1f, 1.1f), new Color(1f, .78f, .22f), .65f);
            AddBurst(focus + new Vector3(5f, 4.2f, .8f), new Color(.25f, .75f, 1f), 1.3f);
        }

        void AddBurst(Vector3 position, Color color, float startAt)
        {
            var objectBurst = new GameObject("烟花爆点");
            objectBurst.transform.SetParent(transform, false);
            objectBurst.transform.position = position;
            var system = objectBurst.AddComponent<ParticleSystem>();
            // A newly added ParticleSystem can already be considered playing
            // before its module properties are configured. Stop and clear it
            // first so changing duration and other main-module values is
            // legal on Unity 6 and does not emit the runtime error seen when
            // the celebration starts.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = 1.8f;
            main.startSpeed = 5.2f;
            main.startSize = .16f;
            main.startColor = color;
            main.gravityModifier = .16f;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .06f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                var material = new Material(shader) { color = color };
                material.name = "Celebration Firework Material";
                renderer.material = material;
            }

            bursts.Add(new Burst { System = system, StartAt = startAt, Emitted = false });
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < bursts.Count; i++)
            {
                var burst = bursts[i];
                if (!burst.Emitted && elapsed >= burst.StartAt)
                {
                    burst.System.Play();
                    burst.System.Emit(48);
                    burst.Emitted = true;
                    bursts[i] = burst;
                }
            }

            if (birthdayText != null)
            {
                float fade = Mathf.Clamp01((elapsed - 3.6f) / 1.4f);
                float alpha = Mathf.Lerp(.98f, 0f, fade);
                birthdayText.color = new Color(1f, .86f, .25f, alpha);
                float pulse = 1f + Mathf.Sin(elapsed * 4f) * .04f;
                birthdayText.transform.localScale = Vector3.one * pulse;
            }
        }
    }
}
