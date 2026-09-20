using System.Collections.Generic;
using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>
    /// Small celebration effect: three particle bursts and an authored
    /// world-space HappyBirthday title. The title is kept in the scene as a
    /// disabled child so its font, wording, transform, and style can be tuned
    /// directly in the Inspector before the effect is played.
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
        [SerializeField] TextMesh birthdayText;
        Color authoredTextColor = Color.white;
        Vector3 authoredTextScale = Vector3.one;
        float elapsed;

        /// <summary>Scene-authored HappyBirthday TextMesh, exposed for the scene builder.</summary>
        public TextMesh BirthdayText
        {
            get { return birthdayText; }
            set { birthdayText = value; }
        }

        public static CelebrationFireworks Create(Vector3 focus)
        {
            // Prefer the disabled scene template. Instantiating it preserves
            // any font and layout edits made in the Inspector while keeping
            // the authored object hidden until the celebration begins.
            var template = FindSceneTemplate();
            if (template != null)
            {
                var cloneObject = Object.Instantiate(template.gameObject);
                cloneObject.name = "庆典烟花 (运行时)";
                cloneObject.SetActive(true);
                var clone = cloneObject.GetComponent<CelebrationFireworks>();
                clone.Initialize(focus, false);
                return clone;
            }

            // Backward-compatible fallback for scenes created before the
            // authored template was added.
            var root = new GameObject("庆典烟花");
            var fireworks = root.AddComponent<CelebrationFireworks>();
            fireworks.Initialize(focus, true);
            return fireworks;
        }

        static CelebrationFireworks FindSceneTemplate()
        {
            var candidates = Object.FindObjectsByType<CelebrationFireworks>(
                FindObjectsInactive.Include);
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.gameObject.activeInHierarchy ||
                    !candidate.gameObject.scene.IsValid())
                    continue;
                if (candidate.transform.Find("HappyBirthday") != null)
                    return candidate;
            }
            return null;
        }

        void Initialize(Vector3 focus, bool createFallbackTitle)
        {
            elapsed = 0f;
            bursts.Clear();

            if (birthdayText == null)
            {
                var title = transform.Find("HappyBirthday");
                if (title != null) birthdayText = title.GetComponent<TextMesh>();
            }
            if (birthdayText == null && createFallbackTitle)
                birthdayText = CreateFallbackTitle(focus);

            if (birthdayText != null)
            {
                authoredTextColor = birthdayText.color;
                if (authoredTextColor.a <= 0f) authoredTextColor.a = 1f;
                authoredTextScale = birthdayText.transform.localScale;
                if (authoredTextScale.sqrMagnitude <= 0.0001f) authoredTextScale = Vector3.one;
                birthdayText.gameObject.SetActive(true);
                birthdayText.color = authoredTextColor;
            }

            AddBurst(focus + new Vector3(-5f, 4.2f, .8f), new Color(1f, .24f, .48f), 0f);
            AddBurst(focus + new Vector3(0f, 5.1f, 1.1f), new Color(1f, .78f, .22f), .65f);
            AddBurst(focus + new Vector3(5f, 4.2f, .8f), new Color(.25f, .75f, 1f), 1.3f);
        }

        TextMesh CreateFallbackTitle(Vector3 focus)
        {
            var titleObject = new GameObject("HappyBirthday");
            titleObject.transform.SetParent(transform, false);
            titleObject.transform.position = focus + Vector3.up * 5.2f + Vector3.back * .2f;
            titleObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            var text = titleObject.AddComponent<TextMesh>();
            text.text = "Happy\nBirthday";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .12f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, .86f, .25f, 1f);
            text.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI", 64);
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
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
                float alpha = authoredTextColor.a * Mathf.Lerp(1f, 0f, fade);
                birthdayText.color = new Color(authoredTextColor.r, authoredTextColor.g,
                    authoredTextColor.b, alpha);
                float pulse = 1f + Mathf.Sin(elapsed * 4f) * .04f;
                birthdayText.transform.localScale = authoredTextScale * pulse;
            }
        }
    }
}
