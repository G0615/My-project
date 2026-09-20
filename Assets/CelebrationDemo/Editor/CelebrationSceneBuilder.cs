#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>
    /// Creates the persistent graybox scene used by the celebration demo.
    /// Only the scene at ScenePath and materials under World/GeneratedMaterials
    /// are owned by this builder; the earlier prototype scene is left alone.
    /// </summary>
    public static class CelebrationSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MaterialFolder = "Assets/CelebrationDemo/World/GeneratedMaterials";
        const string SignFontPath = "Assets/ThirdParty/kenney_ui-pack/Font/Kenney Future.ttf";
        const float CameraPitch = 48f;
        // Keep the operation area at the same apparent scale as the original
        // prototype. The world grew outward around it; the camera should not
        // zoom out with the homes and boundaries.
        const float CameraOrthographicSize = 10.5f;
        const float WorldScale = 2f;
        // Plaza has an 8.8-unit radius.  A 2.9-unit cake sector radius at
        // this scale gives a 7.0-unit cake radius, approximately four-fifths
        // of the plaza diameter while leaving a walkable outer ring.
        const float CakeFootprintScale = 2.43f;

        static readonly Color GroundColor = new Color(0.18f, 0.28f, 0.30f);
        static readonly Color PlazaColor = new Color(0.44f, 0.57f, 0.58f);
        static readonly Color WoodColor = new Color(0.45f, 0.24f, 0.11f);
        static readonly Color Cream = new Color(0.98f, 0.93f, 0.78f);

        [MenuItem("Tools/Celebration Demo/Create Celebration Prototype")]
        public static void Create()
        {
            EnsureFolder("Assets/CelebrationDemo");
            EnsureFolder("Assets/CelebrationDemo/World");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/Scenes");

            Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var oldRoot = FindRoot(scene, GeneratedRootName);
                if (oldRoot == null)
                {
                    Debug.LogError("CelebrationPrototype.unity exists but is not owned by CelebrationSceneBuilder; it was left unchanged.");
                    return;
                }
                Object.DestroyImmediate(oldRoot);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var generated = new GameObject(GeneratedRootName);
            var materials = BuildMaterials();
            BuildLighting(generated.transform);
            BuildEnvironment(generated.transform, materials);

            ActorView[] actors = BuildActors(generated.transform, materials);
            TargetView[] targets = BuildTargets(generated.transform, materials);
            BuildFireworksTemplate(generated.transform);
            FixedAngleCamera cameraRig = BuildCamera(actors[0].transform, generated.transform);

            var runtimeObject = new GameObject("Demo Runtime");
            runtimeObject.transform.SetParent(generated.transform, false);
            var runtime = runtimeObject.AddComponent<DemoRuntime>();
            runtime.Actors = actors;
            runtime.Targets = targets;
            runtime.CameraRig = cameraRig;

            var hudObject = new GameObject("Demo HUD");
            hudObject.transform.SetParent(generated.transform, false);
            var hud = hudObject.AddComponent<DemoHud>();
            runtime.Hud = hud;

            EditorSceneManager.SetActiveScene(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddAsFirstBuildScene(ScenePath);
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = runtimeObject;
            Debug.Log("Created persistent CelebrationPrototype scene with 3 actors and 30 interaction targets.");
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void AddAsFirstBuildScene(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            var output = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes.Length + 1)
            {
                new EditorBuildSettingsScene(path, true)
            };
            foreach (var entry in scenes)
            {
                if (entry.path == path) continue;
                output.Add(entry);
            }
            EditorBuildSettings.scenes = output.ToArray();
        }

        static MaterialSet BuildMaterials()
        {
            return new MaterialSet
            {
                Ground = Material("Ground", GroundColor),
                Plaza = Material("Plaza", PlazaColor),
                Road = Material("Road", new Color(0.16f, 0.18f, 0.2f)),
                White = Material("White", new Color(0.9f, 0.9f, 0.86f)),
                Wood = Material("Wood", WoodColor),
                DarkWood = Material("DarkWood", new Color(0.22f, 0.1f, 0.04f)),
                Green = Material("Green", new Color(0.18f, 0.55f, 0.2f)),
                Leaf = Material("Leaf", new Color(0.12f, 0.42f, 0.12f)),
                Red = Material("Red", ActorView.ColorForActor(1)),
                Yellow = Material("Yellow", ActorView.ColorForActor(2)),
                Blue = Material("Blue", ActorView.ColorForActor(3)),
                Orange = Material("Orange", new Color(0.95f, 0.42f, 0.05f)),
                Pink = Material("Pink", new Color(0.95f, 0.35f, 0.58f)),
                Egg = Material("Egg", new Color(0.98f, 0.92f, 0.72f)),
                Cake = Material("Cake", new Color(0.9f, 0.66f, 0.38f)),
                Cream = Material("Cream", Cream),
                CreamPink = Material("Cream Pink", new Color(1f, 0.46f, 0.7f)),
                CreamBlue = Material("Cream Blue", new Color(0.56f, 0.82f, 1f)),
                CreamPurple = Material("Cream Purple", new Color(0.75f, 0.5f, 1f)),
                Highlight = Material("Highlight", new Color(1f, 0.88f, 0.18f)),
                Tool = Material("Tool", new Color(0.42f, 0.46f, 0.5f)),
                Celebration = Material("Celebration", new Color(0.75f, 0.2f, 0.82f))
            };
        }

        static Material Material(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                SetMaterialColor(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            SetMaterialColor(material, color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        static void BuildLighting(Transform parent)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.94f, 0.86f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.52f);
        }

        static void BuildEnvironment(Transform parent, MaterialSet m)
        {
            // The map footprint and authored travel distances are doubled so
            // the plaza has room for three homes and a separate shop corner.
            CreateVisual("Ground", PrimitiveType.Cube, parent, new Vector3(0f, -0.55f, 0f),
                new Vector3(44f * WorldScale, 1f, 34f * WorldScale), m.Ground, true);
            CreateVisual("Boundary West", PrimitiveType.Cube, parent, new Vector3(-21.8f * WorldScale, 0.5f, 0f),
                new Vector3(0.4f, 1f, 34f * WorldScale), m.DarkWood, true);
            CreateVisual("Boundary East", PrimitiveType.Cube, parent, new Vector3(21.8f * WorldScale, 0.5f, 0f),
                new Vector3(0.4f, 1f, 34f * WorldScale), m.DarkWood, true);
            CreateVisual("Boundary North", PrimitiveType.Cube, parent, new Vector3(0f, 0.5f, 16.8f * WorldScale),
                new Vector3(44f * WorldScale, 1f, 0.4f), m.DarkWood, true);
            CreateVisual("Boundary South", PrimitiveType.Cube, parent, new Vector3(0f, 0.5f, -16.8f * WorldScale),
                new Vector3(44f * WorldScale, 1f, 0.4f), m.DarkWood, true);
            CreateVisual("Main Road", PrimitiveType.Cube, parent, new Vector3(0f, -0.02f, -2.6f * WorldScale),
                new Vector3(40f * WorldScale, 0.12f, 5.2f * WorldScale), m.Road, true);
            // The plaza is a walkable visual surface. Its solid ground below
            // remains the movement collider; a second cylinder here would
            // make the player collide with the stage edge and block entry.
            CreateVisual("Plaza", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.05f, 1.2f * WorldScale),
                new Vector3(8.8f * WorldScale, 0.15f, 8.8f * WorldScale), m.Plaza, false);

            Vector3 homeOne = new Vector3(-30f, 1.1f, 23f);
            Vector3 homeTwo = new Vector3(30f, 1.1f, 23f);
            Vector3 homeThree = new Vector3(-30f, 1.1f, -23f);
            Vector3 shop = new Vector3(30f, 1.1f, -23f);
            CreateBuilding(parent, "Home 1", homeOne, m.Red, m.Green, Vector3.back);
            CreateBuilding(parent, "Home 2", homeTwo, m.Yellow, m.Green, Vector3.back);
            CreateBuilding(parent, "Home 3", homeThree, m.Blue, m.Green, Vector3.back);
            CreateBuilding(parent, "Shop", shop, m.Wood, m.Yellow, Vector3.back);

            CreateSign(parent, "Home 1 Sign", homeOne, Vector3.back, m.Red, "1号家园");
            CreateSign(parent, "Home 2 Sign", homeTwo, Vector3.back, m.Yellow, "2号家园");
            CreateSign(parent, "Home 3 Sign", homeThree, Vector3.back, m.Blue, "3号家园");
            CreateSign(parent, "Shop Sign", shop, Vector3.back, m.Yellow, "鸡蛋商店");

            // The outer trees are authored as interactive targets in
            // BuildTargets below so they use the same F flow as the trees by
            // each door. Keep a marker object to force one migration of older
            // scenes after this layout change.
            var layoutMarker = new GameObject("Interactive Outer Trees Layout V2");
            layoutMarker.transform.SetParent(parent, false);

            Vector3 cakeCenter = new Vector3(0f, 1.2f, 1.2f * WorldScale);
            Vector3 cakeOrigin = new Vector3(cakeCenter.x, 0.05f, cakeCenter.z);
            // Build the cake as six independent 60-degree sectors. The six
            // authored interaction targets below sit on these same sectors,
            // alternating fruit and cream so each slice has its own F zone.
            for (int i = 0; i < 6; i++)
            {
                float centerAngle = i * Mathf.PI / 3f;
                CreateCakeSector(parent, "Cake Sector " + (i + 1) + " Base", cakeOrigin,
                    centerAngle - Mathf.PI / 6f, centerAngle + Mathf.PI / 6f,
                    2.9f * CakeFootprintScale, 1.4f, m.Cake, 7);
                CreateCakeSector(parent, "Cake Sector " + (i + 1) + " Top", cakeOrigin + Vector3.up * 1.4f,
                    centerAngle - Mathf.PI / 6f, centerAngle + Mathf.PI / 6f,
                    2.55f * CakeFootprintScale, 0.12f, m.Cream, 7);
            }
            // The enlarged cake needs a small candle cluster rather than one
            // thin candle that disappears at the new camera distance.
            Vector3[] candleOffsets =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-.65f, 0f, -.45f),
                new Vector3(.65f, 0f, -.45f),
                new Vector3(-.7f, 0f, .5f),
                new Vector3(.7f, 0f, .5f)
            };
            for (int i = 0; i < candleOffsets.Length; i++)
            {
                string suffix = i == 0 ? string.Empty : " " + (i + 1);
                CreateVisual("Cake Candle" + suffix, PrimitiveType.Cylinder,
                    parent, new Vector3(cakeCenter.x, 2.27f, cakeCenter.z) + candleOffsets[i],
                    new Vector3(.18f, .7f, .18f), m.Pink);
                CreateVisual("Cake Flame" + suffix, PrimitiveType.Sphere,
                    parent, new Vector3(cakeCenter.x, 3.13f, cakeCenter.z) + candleOffsets[i],
                    new Vector3(.3f, .45f, .3f), m.Orange);
            }
        }

        static void CreateBuilding(Transform parent, string name, Vector3 position, Material wall, Material roof, Vector3 front)
        {
            CreateVisual(name + " Body", PrimitiveType.Cube, parent, position,
                new Vector3(8.4f, 3f, 6f), wall, true);
            CreateVisual(name + " Roof", PrimitiveType.Cube, parent, position + Vector3.up * 1.8f,
                new Vector3(9f, 0.45f, 6.5f), roof);
            Vector3 direction = front.sqrMagnitude > 0.001f ? front.normalized : Vector3.back;
            var door = CreateVisual(name + " Door", PrimitiveType.Cube, parent,
                position + direction * 3.08f + Vector3.down * 0.15f,
                new Vector3(1.35f, 2.2f, 0.18f), Material("Door", new Color(0.15f, 0.08f, 0.04f)));
            door.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        static void CreateSign(Transform parent, string name, Vector3 buildingPosition, Vector3 front, Material material, string label)
        {
            Vector3 direction = front.sqrMagnitude > 0.001f ? front.normalized : Vector3.back;
            var sign = CreateVisual(name, PrimitiveType.Cube, parent,
                buildingPosition + direction * 3.15f + Vector3.up * 3.7f,
                new Vector3(4.8f, 0.4f, 0.24f), material);
            sign.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            sign.name = name + " [" + label + "]";
            CreateSignText(sign.transform, label);
        }

        static void CreateZoneSign(Transform parent, string name, Vector3 position, string label, MaterialSet m)
        {
            // These signs are deliberately authored as world objects instead
            // of HUD elements: they remain visible while the player switches
            // controlled characters and while the camera follows them.
            CreateVisual(name + " Post", PrimitiveType.Cylinder, parent,
                position + Vector3.up * .68f, new Vector3(.1f, .68f, .1f), m.Wood, false);
            var board = CreateVisual(name + " Board", PrimitiveType.Cube, parent,
                position + Vector3.up * 1.45f + Vector3.back * .02f,
                new Vector3(3.2f, .86f, .16f), m.DarkWood, false);
            board.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            var labelObject = new GameObject(name + " Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.position = position + Vector3.up * 1.45f + Vector3.back * .12f;
            labelObject.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            ConfigureSignText(labelObject.AddComponent<TextMesh>(), label);
        }

        static void CreateSignText(Transform board, string label)
        {
            var labelObject = new GameObject(board.name + " Label");
            labelObject.transform.SetParent(board, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, .15f);
            labelObject.transform.localRotation = Quaternion.identity;
            ConfigureSignText(labelObject.AddComponent<TextMesh>(), label);
        }

        static void ConfigureSignText(TextMesh text, string label)
        {
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = .1f;
            text.color = Color.white;
            text.font = SignFont();
        }

        static Font SignFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SignFontPath);
            if (font != null) return font;
            font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI", 18);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static ActorView[] BuildActors(Transform parent, MaterialSet m)
        {
            var result = new ActorView[3];
            Vector3[] positions =
            {
                new Vector3(-30f, 0f, 19.45f),
                new Vector3(30f, 0f, 19.45f),
                new Vector3(-30f, 0f, -26.35f)
            };
            Material[] colors = { m.Red, m.Yellow, m.Blue };
            for (int i = 0; i < result.Length; i++)
            {
                var actor = new GameObject("Actor " + (i + 1));
                actor.transform.SetParent(parent, false);
                actor.transform.position = positions[i];
                var controller = actor.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.42f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.stepOffset = 0.35f;
                controller.slopeLimit = 45f;
                var view = actor.AddComponent<ActorView>();
                view.ActorId = i + 1;

                CreateVisual("Body", PrimitiveType.Capsule, actor.transform, new Vector3(0f, 0.9f, 0f),
                    new Vector3(0.84f, 0.9f, 0.84f), colors[i]);
                var head = new GameObject("HeadAnchor");
                head.transform.SetParent(actor.transform, false);
                head.transform.localPosition = new Vector3(0f, 2.1f, 0f);
                view.HeadAnchor = head.transform;
                var marker = CreateVisual("SelectedMarker", PrimitiveType.Cylinder, actor.transform,
                    new Vector3(0f, 0.04f, 0f), new Vector3(1.35f, 0.025f, 1.35f), colors[i]);
                marker.SetActive(i == 0);
                result[i] = view;
            }
            return result;
        }

        static CelebrationFireworks BuildFireworksTemplate(Transform parent)
        {
            var root = new GameObject("庆典烟花");
            root.transform.SetParent(parent, false);
            root.SetActive(false);

            var fireworks = root.AddComponent<CelebrationFireworks>();
            var titleObject = new GameObject("HappyBirthday");
            titleObject.transform.SetParent(root.transform, false);
            titleObject.transform.localPosition = new Vector3(0f, 6.4f, 2.2f);
            titleObject.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            titleObject.SetActive(false);

            var text = titleObject.AddComponent<TextMesh>();
            text.text = "Happy\nBirthday";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .12f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, .86f, .25f, 1f);
            text.font = SignFont();
            fireworks.BirthdayText = text;
            return fireworks;
        }

        static TargetView[] BuildTargets(Transform parent, MaterialSet m)
        {
            var targets = new TargetView[30];
            int cursor = 0;

            // Three homes occupy three corners. Each door has a dedicated tree
            // and the matching public pile below uses the same fruit name.
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 1 Apple Tree", TargetKind.HomeFruit,
                "home_apple", new Vector3(-24.6f, -0.08f, 19.7f), "领取苹果", m.Red, m, "Apple");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 2 Banana Tree", TargetKind.HomeBanana,
                "home_banana", new Vector3(24.6f, -0.08f, 19.7f), "领取香蕉", m.Yellow, m, "Banana");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 3 Orange Tree", TargetKind.HomeOrange,
                "home_orange", new Vector3(-24.6f, -0.08f, -26.3f), "领取橘子", m.Orange, m, "Orange");

            // The extra yard trees are also usable fruit sources. They keep
            // their own target IDs so clicking/approaching any tree follows
            // the same public interaction and log path as the door trees.
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 1 Outer Apple Tree A", TargetKind.HomeFruit,
                "home_apple_outer_a", new Vector3(-36.5f, -0.08f, 26.2f), "领取苹果", m.Red, m, "OuterAppleA");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 1 Outer Apple Tree B", TargetKind.HomeFruit,
                "home_apple_outer_b", new Vector3(-37.2f, -0.08f, 20.4f), "领取苹果", m.Red, m, "OuterAppleB");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 2 Outer Banana Tree A", TargetKind.HomeBanana,
                "home_banana_outer_a", new Vector3(36.5f, -0.08f, 26.2f), "领取香蕉", m.Yellow, m, "OuterBananaA");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 2 Outer Banana Tree B", TargetKind.HomeBanana,
                "home_banana_outer_b", new Vector3(37.2f, -0.08f, 20.4f), "领取香蕉", m.Yellow, m, "OuterBananaB");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 3 Outer Orange Tree A", TargetKind.HomeOrange,
                "home_orange_outer_a", new Vector3(-36.5f, -0.08f, -26.4f), "领取橘子", m.Orange, m, "OuterOrangeA");
            targets[cursor++] = AddFruitTreeTarget(parent, "Home 3 Outer Orange Tree B", TargetKind.HomeOrange,
                "home_orange_outer_b", new Vector3(-37.2f, -0.08f, -20.4f), "领取橘子", m.Orange, m, "OuterOrangeB");

            // The shop has one usable egg rack on each side of its south-facing
            // door. Keeping both as real targets makes the two wall displays
            // equivalent ways to buy eggs and avoids a decorative-only dead
            // end when the player approaches from either side.
            targets[cursor++] = AddEggShopTarget(parent, "Shop Eggs Left", "shop_egg_left",
                new Vector3(26.6f, 0f, -26.3f), "Shop Egg Rack Left", m);
            targets[cursor++] = AddEggShopTarget(parent, "Shop Eggs Right", "shop_egg_right",
                new Vector3(33.4f, 0f, -26.3f), "Shop Egg Rack Right", m);

            // Central queue is kept in four compact rows. The authored scene
            // mirrors this queue on the right; every central target is five
            // units farther back than the original prototype layout.
            targets[cursor++] = AddPileTarget(parent, "Apple Pile", TargetKind.FruitPile, "apple_pile",
                new Vector3(-16f, 0.55f, 2.5f), "苹果堆", m.Red, m);
            targets[cursor++] = AddPileTarget(parent, "Banana Pile", TargetKind.BananaPile, "banana_pile",
                new Vector3(-12f, 0.55f, 2.5f), "香蕉堆", m.Yellow, m);
            targets[cursor++] = AddPileTarget(parent, "Orange Pile", TargetKind.OrangePile, "orange_pile",
                new Vector3(-16f, 0.55f, -1.5f), "橘子堆", m.Orange, m);
            targets[cursor++] = AddPileTarget(parent, "Egg Pile", TargetKind.EggPile, "egg_pile",
                new Vector3(-12f, 0.55f, -1.5f), "鸡蛋堆", m.Egg, m);
            targets[cursor++] = AddPileTarget(parent, "Sliced Fruit", TargetKind.SlicedFruit, "sliced_fruit",
                new Vector3(-16f, 0.48f, -11.5f), "果切堆", m.Orange, m);
            targets[cursor++] = AddPileTarget(parent, "Cream Pile", TargetKind.CreamPile, "cream_pile",
                new Vector3(-12f, 0.48f, -11.5f), "奶油堆", m.Cream, m);

            targets[cursor++] = AddStationTarget(parent, "Cut Station", TargetKind.CutStation, "cut",
                new Vector3(-16f, 0.65f, -6f), "切水果", false, m);
            targets[cursor++] = AddStationTarget(parent, "Whip Station", TargetKind.WhipStation, "whip",
                new Vector3(-12f, 0.65f, -6f), "打发奶油", true, m);
            targets[cursor++] = AddChopsticksTarget(parent, m);

            CreateZoneSign(parent, "Donation Zone Sign", new Vector3(-14f, 0f, 2.5f), "捐赠区", m);
            CreateZoneSign(parent, "Processing Zone Sign", new Vector3(-14f, 0f, -1.5f), "加工区", m);
            CreateZoneSign(parent, "Finished Zone Sign", new Vector3(-14f, 0f, -6f), "成品区", m);
            CreateZoneSign(parent, "Trading Zone Sign", new Vector3(-16f, 0f, 6.5f), "交易区", m);
            CreateZoneSign(parent, "Celebration Sign", new Vector3(16f, 0f, 6.5f), "庆典开始", m);

            // Cake targets alternate fruit and cream around the six sectors.
            // Keep the interaction roots within the cake footprint; the
            // cake collider remains the boundary that keeps actors outside.
            const float ringRadius = 2.45f * CakeFootprintScale;
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * ringRadius, 1.2f,
                    2.4f + Mathf.Sin(angle) * ringRadius);
                bool fruit = (i % 2) == 0;
                targets[cursor++] = AddCakeTarget(parent, fruit ? "Cake Fruit " + (i / 2 + 1) : "Cake Cream " + (i / 2 + 1),
                    fruit ? TargetKind.CakeFruit : TargetKind.CakeCream,
                    fruit ? "cake_fruit_" + (i / 2) : "cake_cream_" + (i / 2), i / 2, i,
                    position, fruit ? "贴果切" : "抹奶油", fruit, m);
            }

            targets[cursor++] = AddCelebrationTarget(parent, m);
            // Put each trophy just in front of the south-facing house wall,
            // beside the door.  The three authored points mirror the first
            // home's placement while leaving the doorway clear.
            targets[cursor++] = AddTrophyTarget(parent, 1, new Vector3(-33f, 0f, 19.45f), m);
            targets[cursor++] = AddTrophyTarget(parent, 2, new Vector3(33f, 0f, 19.45f), m);
            targets[cursor++] = AddTrophyTarget(parent, 3, new Vector3(-33f, 0f, -26.55f), m);

            if (cursor != targets.Length)
                Debug.LogError("CelebrationSceneBuilder generated " + cursor + " targets; expected " + targets.Length + ".");
            return targets;
        }

        static TargetView AddFruitTreeTarget(Transform parent, string name, TargetKind kind, string id,
            Vector3 position, string display, Material fruitMaterial, MaterialSet m, string fruitPrefix)
        {
            var target = AddTarget(parent, name, kind, id, 0, 0, position, display, 1.65f, fruitMaterial, m);
            var tree = target.transform;
            CreateVisual("Trunk", PrimitiveType.Cylinder, tree, new Vector3(0f, 0.65f, 0f),
                new Vector3(0.25f, 0.75f, 0.25f), m.Wood, false);
            CreateVisual("Leaves", PrimitiveType.Sphere, tree, new Vector3(0f, 1.65f, 0f),
                new Vector3(1.2f, 1f, 1.2f), m.Leaf, false);
            for (int i = 0; i < 3; i++)
                CreateVisual(fruitPrefix + i, PrimitiveType.Sphere, tree,
                    new Vector3(Mathf.Cos(i * 2.1f) * .55f, 1.25f + (i % 2) * .25f, Mathf.Sin(i * 2.1f) * .55f),
                    Vector3.one * .24f, fruitMaterial, false);
            return target;
        }

        static TargetView AddEggShopTarget(Transform parent, string name, string id, Vector3 position,
            string rackName, MaterialSet m)
        {
            var target = AddTarget(parent, name, TargetKind.ShopEgg, id, 0, 0,
                position, "购买鸡蛋", 1.65f, m.Yellow, m);
            var root = target.transform;
            CreateVisual(rackName, PrimitiveType.Cube, root, new Vector3(0f, .35f, 0f),
                new Vector3(2.4f, .7f, 1.4f), m.Wood, false);
            for (int i = 0; i < 4; i++)
                CreateVisual("Egg" + i, PrimitiveType.Sphere, root,
                    new Vector3((i % 2) * .5f - .25f, .95f, (i / 2) * .45f - .22f),
                    new Vector3(.32f, .42f, .32f), m.Egg, false);
            return target;
        }

        static TargetView AddPileTarget(Transform parent, string name, TargetKind kind, string id,
            Vector3 position, string display, Material pileMaterial, MaterialSet m)
        {
            var target = AddTarget(parent, name, kind, id, 0, 0, position, display, 1.65f, pileMaterial, m);
            var root = target.transform;
            CreateVisual("Basket", PrimitiveType.Cylinder, root, new Vector3(0f, .35f, 0f),
                new Vector3(1.55f, .35f, 1.55f), m.Wood, false);
            for (int i = 0; i < 5; i++)
            {
                Material itemMaterial = pileMaterial;
                string itemName = "Item" + i;
                if (kind == TargetKind.CreamPile)
                {
                    // Match the four colour states used by F on cake cream
                    // regions, so the public cream pile reads as the same
                    // resource players are about to apply.
                    Material[] creamPalette = { m.CreamPink, m.CreamBlue, m.CreamPurple, m.Cream };
                    itemMaterial = creamPalette[i % creamPalette.Length];
                    itemName = "Cream Palette " + (i + 1);
                }
                CreateVisual(itemName, PrimitiveType.Sphere,
                    root, new Vector3(Mathf.Cos(i * 1.25f) * .5f, .8f + (i % 2) * .18f,
                    Mathf.Sin(i * 1.25f) * .5f), kind == TargetKind.EggPile ? new Vector3(.36f, .48f, .36f) : Vector3.one * .35f,
                    itemMaterial, false);
            }
            return target;
        }

        static TargetView AddStationTarget(Transform parent, string name, TargetKind kind, string id,
            Vector3 position, string display, bool whisk, MaterialSet m)
        {
            var target = AddTarget(parent, name, kind, id, 0, 0, position, display, 1.75f, m.Wood, m);
            var root = target.transform;
            CreateVisual("Table", PrimitiveType.Cube, root, new Vector3(0f, .5f, 0f),
                new Vector3(2.5f, 1f, 1.5f), m.Wood, false);
            CreateVisual("Bowl", PrimitiveType.Cylinder, root, new Vector3(0f, 1.12f, 0f),
                new Vector3(.8f, .2f, .8f), whisk ? m.Cream : m.Red, false);
            var tool = CreateVisual("Tool", whisk ? PrimitiveType.Cylinder : PrimitiveType.Cube, root,
                new Vector3(0f, 1.62f, 0f), whisk ? new Vector3(.18f, .8f, .18f) : new Vector3(.18f, .18f, 1.1f),
                m.Tool, false);
            if (whisk) tool.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            return target;
        }

        static TargetView AddChopsticksTarget(Transform parent, MaterialSet m)
        {
            var target = AddTarget(parent, "Chopsticks", TargetKind.Chopsticks, "chopsticks", 0, 0,
                new Vector3(-16f, .65f, 6.5f), "购买限时筷子", 1.6f, m.Pink, m);
            CreateVisual("Cup", PrimitiveType.Cylinder, target.transform, new Vector3(0f, .45f, 0f),
                new Vector3(.72f, .45f, .72f), m.Pink, false);
            for (int i = -1; i <= 1; i++)
                CreateVisual("Stick" + i, PrimitiveType.Cylinder, target.transform,
                    new Vector3(i * .18f, 1.3f, 0f), new Vector3(.06f, .75f, .06f), m.Wood, false);
            return target;
        }

        static TargetView AddCakeTarget(Transform parent, string name, TargetKind kind, string id, int index, int sectorIndex,
            Vector3 position, string display, bool fruit, MaterialSet m)
        {
            var target = AddTarget(parent, name, kind, id, index, 0, position, display, 1.65f,
                fruit ? m.Orange : m.Cream, m);
            var root = target.transform;
            if (!fruit)
            {
                var sector = parent.Find("Cake Sector " + (sectorIndex + 1) + " Top");
                if (sector != null) target.CakeSectorVisual = sector.gameObject;
            }
            // The interaction root is authored inside the cake footprint. Its
            // solid cake collider still keeps actors outside, while the F
            // target itself no longer creates a blocking object around the rim.
            Vector3 attached = Vector3.up * .42f;
            var marker = root.Find("TargetMarker");
            if (marker != null) marker.localPosition = attached;
            if (fruit)
            {
                Vector3[] fruitOffsets =
                {
                    new Vector3(-.55f, 0f, -.35f),
                    new Vector3(0f, 0f, .45f),
                    new Vector3(.55f, 0f, -.35f)
                };
                for (int i = 0; i < fruitOffsets.Length; i++)
                {
                    string stateName = i == 0 ? "StateVisual" : "StateVisual " + (i + 1);
                    var state = CreateVisual(stateName, PrimitiveType.Cylinder, root,
                        attached + fruitOffsets[i], new Vector3(.74f, .04f, .74f), m.DarkWood, false);
                    state.SetActive(false);
                    string fruitName = i == 0 ? "FruitDecoration" : "FruitDecoration " + (i + 1);
                    var decoration = CreateVisual(fruitName, PrimitiveType.Sphere, root,
                        attached + Vector3.up * .10f + fruitOffsets[i],
                        new Vector3(.68f, .16f, .68f), m.Red, false);
                    decoration.SetActive(false);
                }
            }
            else
            {
                // The interactive result is the full 60-degree top sector,
                // linked above through CakeSectorVisual. Do not add a point
                // marker that would suggest only one spot is being frosted.
            }
            return target;
        }

        static GameObject CreateCakeSector(Transform parent, string name, Vector3 origin,
            float startAngle, float endAngle, float radius, float height, Material material, int arcSegments)
        {
            arcSegments = Mathf.Max(2, arcSegments);
            var vertices = new System.Collections.Generic.List<Vector3>(4 + arcSegments * 2);
            var triangles = new System.Collections.Generic.List<int>(arcSegments * 12 + 6);

            // Two centre vertices make a proper top and bottom fan. The arc
            // is sampled into straight segments, which keeps the mesh small
            // while preserving the pizza-slice silhouette at this scale.
            vertices.Add(new Vector3(0f, 0f, 0f));
            vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = i / (float)arcSegments;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, 0f, z));
                vertices.Add(new Vector3(x, height, z));
            }

            for (int i = 0; i < arcSegments; i++)
            {
                int bottomA = 2 + i * 2;
                int topA = bottomA + 1;
                int bottomB = bottomA + 2;
                int topB = bottomA + 3;

                // Bottom and top fans.
                AddTriangle(triangles, 0, bottomA, bottomB);
                AddTriangle(triangles, 1, topB, topA);
                // Curved outer wall.
                AddTriangle(triangles, bottomA, topB, bottomB);
                AddTriangle(triangles, bottomA, topA, topB);
            }

            // The two radial walls close the sector.
            int firstBottom = 2;
            int firstTop = 3;
            int lastBottom = 2 + arcSegments * 2;
            int lastTop = lastBottom + 1;
            AddTriangle(triangles, 0, firstBottom, firstTop);
            AddTriangle(triangles, 0, firstTop, 1);
            AddTriangle(triangles, 0, 1, lastTop);
            AddTriangle(triangles, 0, lastTop, lastBottom);

            var mesh = new Mesh { name = name + " Lit Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var sector = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            sector.transform.SetParent(parent, false);
            sector.transform.localPosition = origin;
            sector.GetComponent<MeshFilter>().sharedMesh = mesh;
            sector.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (name.EndsWith(" Base", System.StringComparison.Ordinal))
            {
                // The six base wedges form the solid cake footprint. A
                // non-trigger mesh collider keeps actors outside the cake
                // while preserving the separate top-sector visuals.
                var collider = sector.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
            return sector;
        }

        static void AddTriangle(System.Collections.Generic.List<int> triangles, int a, int b, int c)
        {
            // The cake material is opaque and back-face culled. Duplicating
            // every triangle with the reverse winding creates two coplanar
            // faces and lets the depth buffer alternate between yellow and
            // black. A correctly wound closed sector already renders its top,
            // curved wall and radial walls without that z-fighting pair.
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        static TargetView AddCelebrationTarget(Transform parent, MaterialSet m)
        {
            var target = AddTarget(parent, "Celebration", TargetKind.Celebration, "celebration", 0, 0,
                new Vector3(16f, .8f, 6.5f), "举办庆典 / 查看结果", 1.75f, m.Celebration, m);
            var root = target.transform;
            CreateVisual("Pedestal", PrimitiveType.Cylinder, root, new Vector3(0f, .7f, 0f),
                new Vector3(1.2f, .7f, 1.2f), m.Wood, false);
            CreateVisual("Banner", PrimitiveType.Cube, root, new Vector3(0f, 2.4f, 0f),
                new Vector3(2.6f, 1.1f, .12f), m.Celebration, false);
            CreateVisual("Pole", PrimitiveType.Cylinder, root, new Vector3(0f, 1.7f, 0f),
                new Vector3(.08f, 1.6f, .08f), m.Wood, false);
            return target;
        }

        static TargetView AddTrophyTarget(Transform parent, int actorId, Vector3 position, MaterialSet m)
        {
            string id = "trophy-" + actorId;
            Material owner = actorId == 1 ? m.Red : actorId == 2 ? m.Yellow : m.Blue;
            var target = AddTarget(parent, "Trophy Stand " + actorId, TargetKind.Trophy, id, 0, actorId,
                position, actorId + "号玩家奖杯 / 查看回顾", 1.65f, owner, m);
            var root = target.transform;
            CreateVisual("Stand", PrimitiveType.Cylinder, root, new Vector3(0f, .35f, 0f),
                new Vector3(1.2f, .35f, 1.2f), m.Wood, false);
            var trophy = CreateVisual("TrophyVisual", PrimitiveType.Cylinder, root,
                new Vector3(0f, 1.25f, 0f), new Vector3(.4f, .8f, .4f), owner, false);
            CreateVisual("TrophyCup", PrimitiveType.Sphere, trophy.transform,
                new Vector3(0f, 1f, 0f), new Vector3(2f, .35f, 2f), owner, false);
            trophy.SetActive(false);
            return target;
        }

        static TargetView AddTarget(Transform parent, string name, TargetKind kind, string id, int index,
            int ownerActorId, Vector3 position, string display, float radius, Material marker, MaterialSet m)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var target = root.AddComponent<TargetView>();
            target.Spec = new TargetSpec
            {
                Id = id,
                Kind = kind,
                Index = index,
                OwnerActorId = ownerActorId
            };
            target.DisplayName = display;
            target.InteractionRadius = radius;
            AddInteractionCollider(root, kind);

            CreateVisual("TargetMarker", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .04f, 0f),
                new Vector3(1.25f, .025f, 1.25f), marker, false);
            var highlight = CreateVisual("Highlight", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .065f, 0f),
                new Vector3(1.45f, .03f, 1.45f), m.Highlight, false);
            highlight.SetActive(false);
            var feedback = new GameObject("FeedbackAnchor");
            feedback.transform.SetParent(root.transform, false);
            feedback.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            target.FeedbackAnchor = feedback.transform;
            return target;
        }

        static void AddInteractionCollider(GameObject root, TargetKind kind)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.name = "InteractionCollider";
            switch (kind)
            {
                case TargetKind.HomeFruit:
                case TargetKind.HomeBanana:
                case TargetKind.HomeOrange:
                    collider.center = new Vector3(0f, 1.1f, 0f);
                    collider.size = new Vector3(2.1f, 2.6f, 2.1f);
                    break;
                case TargetKind.ShopEgg:
                    collider.center = new Vector3(0f, .55f, 0f);
                    collider.size = new Vector3(2.7f, 1.2f, 1.8f);
                    break;
                case TargetKind.CutStation:
                case TargetKind.WhipStation:
                    collider.center = new Vector3(0f, .75f, 0f);
                    collider.size = new Vector3(2.9f, 1.5f, 1.9f);
                    break;
                case TargetKind.CakeFruit:
                case TargetKind.CakeCream:
                    collider.center = new Vector3(0f, .35f, 0f);
                    collider.size = new Vector3(1.15f, .9f, 1.15f);
                    break;
                case TargetKind.Celebration:
                    collider.center = new Vector3(0f, .8f, 0f);
                    collider.size = new Vector3(1.7f, 1.8f, 1.7f);
                    break;
                case TargetKind.Trophy:
                    collider.center = new Vector3(0f, .5f, 0f);
                    collider.size = new Vector3(1.5f, 1.1f, 1.5f);
                    break;
                case TargetKind.Chopsticks:
                    collider.center = new Vector3(0f, .75f, 0f);
                    collider.size = new Vector3(1.1f, 1.6f, 1.1f);
                    break;
                default:
                    collider.center = new Vector3(0f, .55f, 0f);
                    collider.size = new Vector3(1.9f, 1.2f, 1.9f);
                    break;
            }
        }

        static FixedAngleCamera BuildCamera(Transform initialTarget, Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 behind = -(cameraObject.transform.rotation * Vector3.forward).normalized * 21f;
            cameraObject.transform.position = initialTarget.position + behind + Vector3.up;
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.14f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            if (cameraObject.GetComponent<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
            return cameraObject.AddComponent<FixedAngleCamera>();
        }

        static GameObject CreateVisual(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider = false)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!keepCollider)
            {
                var collider = visual.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            return visual;
        }

        struct MaterialSet
        {
            public Material Ground;
            public Material Plaza;
            public Material Road;
            public Material White;
            public Material Wood;
            public Material DarkWood;
            public Material Green;
            public Material Leaf;
            public Material Red;
            public Material Yellow;
            public Material Blue;
            public Material Orange;
            public Material Pink;
            public Material Egg;
            public Material Cake;
            public Material Cream;
            public Material CreamPink;
            public Material CreamBlue;
            public Material CreamPurple;
            public Material Highlight;
            public Material Tool;
            public Material Celebration;
        }
    }
}
#endif
