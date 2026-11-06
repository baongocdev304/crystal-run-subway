using System.Collections.Generic;
using System.IO;
using CrystalRun;
using CrystalRun.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrystalRun.Editor
{
    /// <summary>
    /// Công cụ Editor tự động dựng toàn bộ dự án CrystalRun phiên bản "Subway Surfers: Tokyo Night Neon":
    /// - Menu: "Tools/Crystal Run/Build All".
    /// - Thiết lập Tags & Layers trong ProjectSettings/TagManager.asset.
    /// - Tạo bộ Materials URP Lit với độ bóng (Smoothness), kim loại (Metallic) và đèn Neon phát sáng (Emission).
    /// - Dựng môi trường đường sắt 3 làn (tà vẹt gỗ, 2 thanh ray thép mỗi làn), tường chắn, cổng điện cao thế và dãy phố Tokyo ban đêm rực rỡ ánh đèn Neon.
    /// - Dựng nhân vật Street Skater đa khớp (Áo Hoodie, Mũ lưỡi trai đội ngược, Balo bình xịt, Giày Sneaker, Ván trượt Hoverboard) kèm PlayerVisualAnimator.
    /// - Dựng hệ thống Toa tàu điện đứng yên & Đoàn tàu chạy ngược chiều (MovingTrain) có sàn nóc tàu (RoofFloor ở layer Ground) và Dốc leo lên nóc tàu (Train_Ramp).
    /// - Xếp xu vàng theo đường cong Parabol (SpawnCoinArc) qua rào chắn và trên nóc tàu.
    /// </summary>
    public static class CrystalRunBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoBuildIfMissingTokyoAssets()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorApplication.isCompiling ||
                    EditorApplication.isUpdating)
                {
                    return;
                }

                // Tự động dựng phiên bản Subway Surfers Tokyo nếu chưa có prefab đoàn tàu mới
                if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Train_Blue.prefab") == null)
                {
                    BuildAll();
                }
            };
        }

        [MenuItem("Tools/Crystal Run/Build All", priority = 0)]
        public static void BuildAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang cấu hình Tags và Layers...", 0.1f);
                SetupTagsAndLayers();

                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang tạo Materials Subway Surfers Tokyo Neon...", 0.25f);
                EnsureDirectory("Assets/Materials");
                Dictionary<string, Material> materials = SetupMaterials();

                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang tạo GameConfig...", 0.4f);
                GameConfig config = SetupGameConfig();

                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang dựng Prefabs (Nhân vật, Tàu điện, Rào chắn, Đoạn đường)...", 0.6f);
                EnsureDirectory("Assets/Prefabs");
                PrefabManifest manifest = SetupPrefabs(config, materials);

                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang tạo Scene Main.unity...", 0.85f);
                EnsureDirectory("Assets/Scenes");
                SetupScene(config, manifest);

                EditorUtility.DisplayProgressBar("CrystalRun Builder", "Đang cập nhật Build Settings...", 0.95f);
                SetupBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "CrystalRun Builder - Subway Surfers Tokyo Neon",
                        "Đã dựng thành công phiên bản Subway Surfers: Tokyo Night Neon!\n\n" +
                        "- Đường ray 3 làn (tà vẹt gỗ + ray thép) & Phố đêm Tokyo Neon.\n" +
                        "- Nhân vật Street Skater có hoạt ảnh chạy, nghiêng làn, lộn vòng & Hoverboard.\n" +
                        "- Toa tàu đứng yên, Tàu chạy ngược chiều, Dốc leo nóc tàu & Rào chắn Đỏ-Trắng.\n" +
                        "- Xu xếp đường cong Parabol & Camera Dynamic FOV + Shake.\n\n" +
                        "Hãy mở Scene Main.unity và bấm Play để trải nghiệm!",
                        "Chơi ngay"
                    );
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region 1. Tags và Layers

        private static void SetupTagsAndLayers()
        {
            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0) return;

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            string[] requiredTags = new string[] { "Player", "Coin", "Crystal", "PowerUp", "Obstacle", "Pit" };
            foreach (string tag in requiredTags)
            {
                bool exists = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                }
            }

            string[] requiredLayers = new string[] { "Player", "Obstacle", "Ground" };
            foreach (string layer in requiredLayers)
            {
                bool exists = false;
                for (int i = 8; i < 32; i++)
                {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                    if (sp != null && sp.stringValue == layer)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    for (int i = 8; i < 32; i++)
                    {
                        SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                        if (sp != null && string.IsNullOrEmpty(sp.stringValue))
                        {
                            sp.stringValue = layer;
                            break;
                        }
                    }
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        #endregion

        #region 2. Materials (Subway Surfers Tokyo Night & Vibrant Palette)

        private static Dictionary<string, Material> SetupMaterials()
        {
            var dict = new Dictionary<string, Material>();

            // 1. Nền đường sắt & Hạ tầng
            dict["Ballast"] = GetOrCreateMaterial("Mat_Ballast", HexColor("#353342"), 0.05f, 0.15f);
            dict["Ground"] = dict["Ballast"];
            dict["WoodTie"] = GetOrCreateMaterial("Mat_WoodTie", HexColor("#6E472B"), 0.05f, 0.22f);
            dict["SteelRail"] = GetOrCreateMaterial("Mat_SteelRail", HexColor("#C8D2DC"), 0.82f, 0.78f);
            dict["BrickWall"] = GetOrCreateMaterial("Mat_BrickWall", HexColor("#6D3B36"), 0.05f, 0.25f);
            dict["Border"] = dict["BrickWall"];

            // 2. Toa tàu điện (Subway Trains)
            dict["TrainBody_Blue"] = GetOrCreateMaterial("Mat_TrainBody_Blue", HexColor("#1D63B8"), 0.25f, 0.72f);
            dict["TrainBody_Red"] = GetOrCreateMaterial("Mat_TrainBody_Red", HexColor("#D9383A"), 0.25f, 0.72f);
            dict["TrainBody_Yellow"] = GetOrCreateMaterial("Mat_TrainBody_Yellow", HexColor("#F5B82E"), 0.25f, 0.72f);
            dict["TrainStripe"] = GetOrCreateMaterial("Mat_TrainStripe", HexColor("#F4F6F8"), 0.1f, 0.70f);
            dict["TrainRoof"] = GetOrCreateMaterial("Mat_TrainRoof", HexColor("#454D5E"), 0.35f, 0.50f);
            dict["TrainWindow"] = GetOrCreateMaterial("Mat_TrainWindow", HexColor("#16222F"), 0.2f, 0.90f, true, HexColor("#1E6091") * 0.85f);
            dict["TrainHeadlight"] = GetOrCreateMaterial("Mat_TrainHeadlight", HexColor("#FFFCE0"), 0.1f, 0.9f, true, HexColor("#FFF8B0") * 3.2f);
            dict["Ramp_Wood"] = GetOrCreateMaterial("Mat_Ramp_Wood", HexColor("#E87A24"), 0.1f, 0.45f, true, HexColor("#FF8C00") * 0.25f);

            // 3. Rào chắn Đỏ - Trắng (Track Barriers)
            dict["Barrier_Red"] = GetOrCreateMaterial("Mat_Barrier_Red", HexColor("#E62E2D"), 0.1f, 0.65f, true, HexColor("#E62E2D") * 0.2f);
            dict["Barrier_White"] = GetOrCreateMaterial("Mat_Barrier_White", HexColor("#F5F5F5"), 0.1f, 0.65f);
            dict["Barrier_Metal"] = GetOrCreateMaterial("Mat_Barrier_Metal", HexColor("#3A3F45"), 0.65f, 0.55f);

            // 4. Tòa nhà Phố thị & Đèn Neon Tokyo
            dict["Building_1"] = GetOrCreateMaterial("Mat_Building_1", HexColor("#FF7A59"), 0.05f, 0.35f);
            dict["Building_2"] = GetOrCreateMaterial("Mat_Building_2", HexColor("#FFC845"), 0.05f, 0.35f);
            dict["Building_3"] = GetOrCreateMaterial("Mat_Building_3", HexColor("#2A9D8F"), 0.05f, 0.35f);
            dict["Building_4"] = GetOrCreateMaterial("Mat_Building_4", HexColor("#8E5572"), 0.05f, 0.35f);

            dict["Neon_Pink"] = GetOrCreateMaterial("Mat_Neon_Pink", HexColor("#FF2A85"), 0.1f, 0.85f, true, HexColor("#FF1E8E") * 2.6f);
            dict["Neon_Cyan"] = GetOrCreateMaterial("Mat_Neon_Cyan", HexColor("#00F0FF"), 0.1f, 0.85f, true, HexColor("#00F0FF") * 2.6f);
            dict["Neon_Yellow"] = GetOrCreateMaterial("Mat_Neon_Yellow", HexColor("#FFE600"), 0.1f, 0.85f, true, HexColor("#FFD900") * 2.4f);
            dict["Neon_Purple"] = GetOrCreateMaterial("Mat_Neon_Purple", HexColor("#B52BFF"), 0.1f, 0.85f, true, HexColor("#B52BFF") * 2.4f);
            dict["Window_Lit"] = GetOrCreateMaterial("Mat_Window_Lit", HexColor("#FFE9A8"), 0.1f, 0.8f, true, HexColor("#FFD27D") * 1.5f);

            // 5. Nhân vật Street Skater
            dict["Player"] = GetOrCreateMaterial("Mat_Player", HexColor("#E63946"), 0.1f, 0.45f);
            dict["Player_Jeans"] = GetOrCreateMaterial("Mat_Player_Jeans", HexColor("#1D3557"), 0.05f, 0.30f);
            dict["Player_Skin"] = GetOrCreateMaterial("Mat_Player_Skin", HexColor("#FFDCB8"), 0.05f, 0.35f);
            dict["Player_Cap"] = GetOrCreateMaterial("Mat_Player_Cap", HexColor("#F8F9FA"), 0.1f, 0.50f);
            dict["Player_Backpack"] = GetOrCreateMaterial("Mat_Player_Backpack", HexColor("#FFB703"), 0.3f, 0.65f);

            // 6. Chướng ngại vật bổ trợ & Hố sâu
            dict["Obstacle_Low"] = dict["Barrier_Red"];
            dict["Obstacle_Overhead"] = dict["Barrier_Red"];
            dict["Obstacle_Boulder"] = GetOrCreateMaterial("Mat_Obstacle_Boulder", HexColor("#2B303A"), 0.75f, 0.70f, true, HexColor("#FF2A85") * 0.9f);
            dict["Pit"] = GetOrCreateMaterial("Mat_Pit", HexColor("#080811"), 0.0f, 0.1f);

            // 7. Vật phẩm thu thập (Xu, Tinh thể, Power-Up)
            dict["Coin"] = GetOrCreateMaterial("Mat_Coin", HexColor("#FFD700"), 0.60f, 0.82f, true, HexColor("#FFA500") * 1.35f);
            dict["Crystal"] = GetOrCreateMaterial("Mat_Crystal", HexColor("#00F5FF"), 0.25f, 0.90f, true, HexColor("#00F5FF") * 2.2f);
            dict["PowerUp_Shield"] = GetOrCreateMaterial("Mat_PowerUp_Shield", HexColor("#2088FF"), 0.3f, 0.8f, true, HexColor("#2088FF") * 1.6f);
            dict["PowerUp_Magnet"] = GetOrCreateMaterial("Mat_PowerUp_Magnet", HexColor("#FF304F"), 0.3f, 0.8f, true, HexColor("#FF304F") * 1.6f);
            dict["PowerUp_DoubleScore"] = GetOrCreateMaterial("Mat_PowerUp_DoubleScore", HexColor("#C838FF"), 0.3f, 0.8f, true, HexColor("#C838FF") * 1.6f);

            // 8. Vòng bảo vệ & Ván trượt Hoverboard
            dict["Shield_Aura"] = GetOrCreateMaterial("Mat_Shield_Aura", new Color(0.15f, 0.80f, 1.0f, 0.32f), 0.1f, 0.85f, true, new Color(0.15f, 0.80f, 1.0f) * 0.9f, true);

            return dict;
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                return c;
            }
            return Color.white;
        }

        private static Material GetOrCreateMaterial(
            string matName,
            Color color,
            float metallic = 0.0f,
            float smoothness = 0.5f,
            bool isEmission = false,
            Color emissionColor = default,
            bool isTransparent = false)
        {
            string path = $"Assets/Materials/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (isEmission)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            if (isTransparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        #endregion

        #region 3. GameConfig Asset

        private static GameConfig SetupGameConfig()
        {
            string path = "Assets/GameConfig.asset";
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        #endregion

        #region 4. Prefabs (Nhân vật Street Skater, Tàu điện, Rào chắn, Đoạn đường)

        private class PrefabManifest
        {
            public GameObject playerPrefab;
            public GameObject coinPrefab;
            public GameObject crystalPrefab;
            public GameObject shieldPowerUpPrefab;
            public GameObject magnetPowerUpPrefab;
            public GameObject doubleScorePowerUpPrefab;

            public GameObject obstacleLowPrefab;
            public GameObject obstacleOverheadPrefab;
            public GameObject obstaclePitPrefab;
            public GameObject obstacleBoulderPrefab;

            public GameObject trainBluePrefab;
            public GameObject trainYellowPrefab;
            public GameObject movingTrainRedPrefab;
            public GameObject trainRampPrefab;

            public List<TrackSegment> safeSegments = new List<TrackSegment>();
            public List<TrackSegment> stage1Segments = new List<TrackSegment>();
            public List<TrackSegment> stage2Segments = new List<TrackSegment>();
            public List<TrackSegment> stage3Segments = new List<TrackSegment>();
        }

        private static PrefabManifest SetupPrefabs(GameConfig config, Dictionary<string, Material> mats)
        {
            var manifest = new PrefabManifest();

            // 1. Nhân vật Street Skater
            manifest.playerPrefab = BuildPlayerPrefab(config, mats);

            // 2. Vật phẩm
            manifest.coinPrefab = BuildCoinPrefab(config, mats);
            manifest.crystalPrefab = BuildCrystalPrefab(config, mats["Crystal"]);
            manifest.shieldPowerUpPrefab = BuildPowerUpPrefab(PowerUpType.Shield, mats["PowerUp_Shield"], mats["Neon_Cyan"]);
            manifest.magnetPowerUpPrefab = BuildPowerUpPrefab(PowerUpType.Magnet, mats["PowerUp_Magnet"], mats["Barrier_White"]);
            manifest.doubleScorePowerUpPrefab = BuildPowerUpPrefab(PowerUpType.DoubleScore, mats["PowerUp_DoubleScore"], mats["Neon_Yellow"]);

            // 3. Chướng ngại vật & Đoàn tàu Subway Surfers
            manifest.obstacleLowPrefab = BuildObstacleLow(mats);
            manifest.obstacleOverheadPrefab = BuildObstacleOverhead(mats);
            manifest.obstaclePitPrefab = BuildObstaclePit(mats["Pit"]);
            manifest.obstacleBoulderPrefab = BuildObstacleBoulder(mats);

            manifest.trainBluePrefab = BuildSubwayTrainPrefab("Obstacle_Train_Blue", mats["TrainBody_Blue"], mats, false);
            manifest.trainYellowPrefab = BuildSubwayTrainPrefab("Obstacle_Train_Yellow", mats["TrainBody_Yellow"], mats, false);
            manifest.movingTrainRedPrefab = BuildSubwayTrainPrefab("Obstacle_MovingTrain", mats["TrainBody_Red"], mats, true);
            manifest.trainRampPrefab = BuildTrainRampPrefab(mats);

            // 4. Các biến thể Đoạn đường (Track Segments)
            manifest.safeSegments.Add(BuildTrackSegmentSafe(manifest, mats));

            // Giai đoạn 1 (Rào Đỏ-Trắng, Tàu đứng yên kèm dốc lên nóc & hàng xu đường cong)
            manifest.stage1Segments.Add(BuildTrackSegmentS1_BarriersAndArcs(manifest, mats));
            manifest.stage1Segments.Add(BuildTrackSegmentS1_SingleTrainWithRamp(manifest, mats));
            manifest.stage1Segments.Add(BuildTrackSegmentS1_SideTrainAndBarriers(manifest, mats));

            // Giai đoạn 2 (2 Tàu song song nhảy qua nóc, Hố đường ray, Rào trên nóc tàu)
            manifest.stage2Segments.Add(BuildTrackSegmentS2_DualTrainsRooftopJump(manifest, mats));
            manifest.stage2Segments.Add(BuildTrackSegmentS2_PitAndRollBarriers(manifest, mats));
            manifest.stage2Segments.Add(BuildTrackSegmentS2_RooftopObstacles(manifest, mats));

            // Giai đoạn 3 (Đoàn tàu chạy ngược chiều MovingTrain, Shibuya Rush Hour Gauntlet)
            manifest.stage3Segments.Add(BuildTrackSegmentS3_OncomingExpress(manifest, mats));
            manifest.stage3Segments.Add(BuildTrackSegmentS3_DualMovingTrains(manifest, mats));
            manifest.stage3Segments.Add(BuildTrackSegmentS3_TokyoRushGauntlet(manifest, mats));

            return manifest;
        }

        private static GameObject CreateVisualPart(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            Vector3 localEuler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            MeshRenderer mr = part.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Collider col = part.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            return part;
        }

        private static GameObject BuildPlayerPrefab(GameConfig config, Dictionary<string, Material> mats)
        {
            GameObject go = new GameObject("Player");
            go.tag = "Player";

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1) go.layer = playerLayer;

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.38f;
            col.center = new Vector3(0f, 0.9f, 0f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            PlayerController controller = go.AddComponent<PlayerController>();
            controller.SetConfig(config);

            PlayerHealth health = go.AddComponent<PlayerHealth>();
            health.SetConfig(config);

            // === Cấu trúc nhân vật Street Skater (VisualRoot) ===
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(go.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;

            // 1. Thân áo Hoodie & Balo bình xịt sơn
            GameObject torso = CreateVisualPart(PrimitiveType.Cube, "Torso", visualRoot.transform,
                new Vector3(0f, 0.96f, 0f), new Vector3(0.50f, 0.60f, 0.30f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Cylinder, "HoodieCollar", torso.transform,
                new Vector3(0f, 0.52f, -0.08f), new Vector3(0.76f, 0.14f, 0.76f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Cube, "Backpack", torso.transform,
                new Vector3(0f, 0.04f, -0.72f), new Vector3(0.72f, 0.72f, 0.55f), mats["Player_Backpack"]);
            CreateVisualPart(PrimitiveType.Cylinder, "SprayCan", torso.transform,
                new Vector3(0.45f, 0.05f, -0.72f), new Vector3(0.22f, 0.30f, 0.22f), mats["Neon_Cyan"]);

            // 2. Đầu & Mũ lưỡi trai đội ngược
            GameObject head = CreateVisualPart(PrimitiveType.Sphere, "Head", visualRoot.transform,
                new Vector3(0f, 1.48f, 0f), new Vector3(0.42f, 0.44f, 0.42f), mats["Player_Skin"]);
            CreateVisualPart(PrimitiveType.Sphere, "CapCrown", head.transform,
                new Vector3(0f, 0.22f, 0f), new Vector3(1.04f, 0.65f, 1.04f), mats["Player_Cap"]);
            CreateVisualPart(PrimitiveType.Cube, "CapVisorBack", head.transform,
                new Vector3(0f, 0.12f, -0.58f), new Vector3(0.68f, 0.10f, 0.55f), mats["Player"], new Vector3(-10f, 0f, 0f));

            // 3. Khớp Tay Trái & Phải
            GameObject leftArm = new GameObject("LeftArm");
            leftArm.transform.SetParent(visualRoot.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.34f, 1.20f, 0f);
            CreateVisualPart(PrimitiveType.Capsule, "UpperArm", leftArm.transform,
                new Vector3(0f, -0.20f, 0f), new Vector3(0.16f, 0.22f, 0.16f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Sphere, "Hand", leftArm.transform,
                new Vector3(0f, -0.44f, 0f), new Vector3(0.15f, 0.15f, 0.15f), mats["Player_Skin"]);

            GameObject rightArm = new GameObject("RightArm");
            rightArm.transform.SetParent(visualRoot.transform, false);
            rightArm.transform.localPosition = new Vector3(0.34f, 1.20f, 0f);
            CreateVisualPart(PrimitiveType.Capsule, "UpperArm", rightArm.transform,
                new Vector3(0f, -0.20f, 0f), new Vector3(0.16f, 0.22f, 0.16f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Sphere, "Hand", rightArm.transform,
                new Vector3(0f, -0.44f, 0f), new Vector3(0.15f, 0.15f, 0.15f), mats["Player_Skin"]);

            // 4. Khớp Chân Trái & Phải kèm Giày Sneaker
            GameObject leftLeg = new GameObject("LeftLeg");
            leftLeg.transform.SetParent(visualRoot.transform, false);
            leftLeg.transform.localPosition = new Vector3(-0.14f, 0.66f, 0f);
            CreateVisualPart(PrimitiveType.Capsule, "LegMesh", leftLeg.transform,
                new Vector3(0f, -0.28f, 0f), new Vector3(0.19f, 0.28f, 0.19f), mats["Player_Jeans"]);
            CreateVisualPart(PrimitiveType.Cube, "Sneaker", leftLeg.transform,
                new Vector3(0f, -0.58f, 0.05f), new Vector3(0.22f, 0.14f, 0.34f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Cube, "SneakerSole", leftLeg.transform,
                new Vector3(0f, -0.64f, 0.05f), new Vector3(0.24f, 0.04f, 0.36f), mats["Barrier_White"]);

            GameObject rightLeg = new GameObject("RightLeg");
            rightLeg.transform.SetParent(visualRoot.transform, false);
            rightLeg.transform.localPosition = new Vector3(0.14f, 0.66f, 0f);
            CreateVisualPart(PrimitiveType.Capsule, "LegMesh", rightLeg.transform,
                new Vector3(0f, -0.28f, 0f), new Vector3(0.19f, 0.28f, 0.19f), mats["Player_Jeans"]);
            CreateVisualPart(PrimitiveType.Cube, "Sneaker", rightLeg.transform,
                new Vector3(0f, -0.58f, 0.05f), new Vector3(0.22f, 0.14f, 0.34f), mats["Player"]);
            CreateVisualPart(PrimitiveType.Cube, "SneakerSole", rightLeg.transform,
                new Vector3(0f, -0.64f, 0.05f), new Vector3(0.24f, 0.04f, 0.36f), mats["Barrier_White"]);

            // Gắn PlayerVisualAnimator
            PlayerVisualAnimator animator = go.AddComponent<PlayerVisualAnimator>();
            animator.ConfigureParts(controller, visualRoot.transform, torso.transform, head.transform,
                leftArm.transform, rightArm.transform, leftLeg.transform, rightLeg.transform);

            // 5. Ván trượt Hoverboard & Vòng Khiên Bảo vệ (ShieldVisual)
            GameObject shieldObj = new GameObject("ShieldVisual");
            shieldObj.transform.SetParent(go.transform, false);
            shieldObj.transform.localPosition = Vector3.zero;

            // Tấm ván trượt Hoverboard phát sáng dưới chân
            CreateVisualPart(PrimitiveType.Cube, "HoverboardDeck", shieldObj.transform,
                new Vector3(0f, 0.05f, 0f), new Vector3(0.82f, 0.07f, 1.65f), mats["Neon_Pink"]);
            CreateVisualPart(PrimitiveType.Cylinder, "HoverThrusterFront", shieldObj.transform,
                new Vector3(0f, 0.03f, 0.55f), new Vector3(0.55f, 0.04f, 0.55f), mats["Neon_Cyan"]);
            CreateVisualPart(PrimitiveType.Cylinder, "HoverThrusterBack", shieldObj.transform,
                new Vector3(0f, 0.03f, -0.55f), new Vector3(0.55f, 0.04f, 0.55f), mats["Neon_Cyan"]);
            CreateVisualPart(PrimitiveType.Sphere, "ShieldAuraDome", shieldObj.transform,
                new Vector3(0f, 0.95f, 0f), new Vector3(1.55f, 2.15f, 1.55f), mats["Shield_Aura"]);

            shieldObj.SetActive(false);

            string path = "Assets/Prefabs/Player.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildCoinPrefab(GameConfig config, Dictionary<string, Material> mats)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Coin";
            go.tag = "Coin";

            go.transform.localScale = new Vector3(0.82f, 0.09f, 0.82f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mats["Coin"];

            // Chi tiết lõi ngôi sao giữa đồng xu
            CreateVisualPart(PrimitiveType.Cube, "CoinCore", go.transform,
                Vector3.zero, new Vector3(0.45f, 1.25f, 0.45f), mats["Neon_Yellow"], new Vector3(0f, 45f, 0f));

            Collider col = go.GetComponent<Collider>();
            col.isTrigger = true;

            Coin coin = go.AddComponent<Coin>();
            coin.SetConfig(config);

            string path = "Assets/Prefabs/Coin.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildCrystalPrefab(GameConfig config, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Crystal";
            go.tag = "Crystal";

            go.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
            go.transform.rotation = Quaternion.Euler(45f, 45f, 45f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            CreateVisualPart(PrimitiveType.Cube, "CrystalRing", go.transform,
                Vector3.zero, new Vector3(1.1f, 0.6f, 1.1f), mat, new Vector3(0f, 45f, 0f));

            Collider col = go.GetComponent<Collider>();
            col.isTrigger = true;

            Crystal crystal = go.AddComponent<Crystal>();
            crystal.SetConfig(config);

            string path = "Assets/Prefabs/Crystal.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildPowerUpPrefab(PowerUpType type, Material mainMat, Material accentMat)
        {
            string name = $"PowerUp_{type}";
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.tag = "PowerUp";
            go.transform.localScale = Vector3.one * 0.78f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mainMat;

            // Vòng xoay neon bao quanh Power-Up
            CreateVisualPart(PrimitiveType.Cube, "AccentRing", go.transform,
                Vector3.zero, new Vector3(1.35f, 0.18f, 1.35f), accentMat, new Vector3(25f, 45f, 0f));

            Collider col = go.GetComponent<Collider>();
            col.isTrigger = true;

            PowerUp pu = go.AddComponent<PowerUp>();
            pu.SetType(type);

            string path = $"Assets/Prefabs/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Rào chắn đường sắt thấp Đỏ - Trắng (Low Barrier - Nhảy qua).
        /// </summary>
        private static GameObject BuildObstacleLow(Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject("Obstacle_Low");
            root.tag = "Obstacle";

            int obsLayer = LayerMask.NameToLayer("Obstacle");
            if (obsLayer != -1) root.layer = obsLayer;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.50f, 0f);
            box.size = new Vector3(2.15f, 1.0f, 0.45f);

            Obstacle obs = root.AddComponent<Obstacle>();
            obs.Configure(ObstacleType.Low, 1);

            // 2 chân đế kim loại
            CreateVisualPart(PrimitiveType.Cube, "LegLeft", root.transform,
                new Vector3(-0.90f, 0.48f, 0f), new Vector3(0.14f, 0.96f, 0.40f), mats["Barrier_Metal"]);
            CreateVisualPart(PrimitiveType.Cube, "LegRight", root.transform,
                new Vector3(0.90f, 0.48f, 0f), new Vector3(0.14f, 0.96f, 0.40f), mats["Barrier_Metal"]);

            // Thanh chắn sọc Đỏ - Trắng xen kẽ (5 khối)
            for (int i = -2; i <= 2; i++)
            {
                Material stripeMat = (Mathf.Abs(i) % 2 == 0) ? mats["Barrier_Red"] : mats["Barrier_White"];
                CreateVisualPart(PrimitiveType.Cube, $"Stripe_{i + 2}", root.transform,
                    new Vector3(i * 0.42f, 0.68f, 0f), new Vector3(0.42f, 0.44f, 0.22f), stripeMat);
            }

            // 2 đèn cảnh báo phát sáng trên đỉnh rào
            CreateVisualPart(PrimitiveType.Sphere, "WarnLightL", root.transform,
                new Vector3(-0.90f, 1.02f, 0f), new Vector3(0.20f, 0.20f, 0.20f), mats["Neon_Yellow"]);
            CreateVisualPart(PrimitiveType.Sphere, "WarnLightR", root.transform,
                new Vector3(0.90f, 1.02f, 0f), new Vector3(0.20f, 0.20f, 0.20f), mats["Neon_Yellow"]);

            string path = "Assets/Prefabs/Obstacle_Low.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Rào chắn cao hở gầm Đỏ - Trắng (High Roll Barrier - Trượt chui qua gầm 0m - 1.08m).
        /// </summary>
        private static GameObject BuildObstacleOverhead(Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject("Obstacle_Overhead");
            root.tag = "Obstacle";

            int obsLayer = LayerMask.NameToLayer("Obstacle");
            if (obsLayer != -1) root.layer = obsLayer;

            // Collider chỉ nằm ở phần trên (Y từ 1.08m đến 2.30m) để nhân vật trượt (cao 0.6m) chui lọt an toàn
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.69f, 0f);
            box.size = new Vector3(2.15f, 1.22f, 0.45f);

            Obstacle obs = root.AddComponent<Obstacle>();
            obs.Configure(ObstacleType.Overhead, 1);

            // 2 cột trụ cao hai bên
            CreateVisualPart(PrimitiveType.Cube, "PostLeft", root.transform,
                new Vector3(-1.02f, 1.18f, 0f), new Vector3(0.15f, 2.36f, 0.35f), mats["Barrier_Metal"]);
            CreateVisualPart(PrimitiveType.Cube, "PostRight", root.transform,
                new Vector3(1.02f, 1.18f, 0f), new Vector3(0.15f, 2.36f, 0.35f), mats["Barrier_Metal"]);

            // Bảng chắn sọc Đỏ - Trắng ở tầng cao
            for (int i = -2; i <= 2; i++)
            {
                Material stripeMat = (Mathf.Abs(i) % 2 == 0) ? mats["Barrier_Red"] : mats["Barrier_White"];
                CreateVisualPart(PrimitiveType.Cube, $"HighStripe_{i + 2}", root.transform,
                    new Vector3(i * 0.42f, 1.68f, 0f), new Vector3(0.42f, 0.95f, 0.24f), stripeMat);
            }

            // Thanh viền neon cảnh báo gầm thấp
            CreateVisualPart(PrimitiveType.Cube, "BottomNeonBar", root.transform,
                new Vector3(0f, 1.14f, 0f), new Vector3(2.15f, 0.08f, 0.28f), mats["Neon_Yellow"]);

            string path = "Assets/Prefabs/Obstacle_Overhead.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Toa tàu điện Subway Surfers (Đứng yên hoặc Chạy ngược chiều) có sàn nóc tàu chạy được ở Y = 2.6m.
        /// </summary>
        private static GameObject BuildSubwayTrainPrefab(
            string prefabName,
            Material bodyMat,
            Dictionary<string, Material> mats,
            bool isMovingTrain)
        {
            GameObject root = new GameObject(prefabName);
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            int groundLayer = LayerMask.NameToLayer("Ground");

            // 1. Collider thân tàu (từ Y = 0.05m đến Y = 2.25m) -> Va vào mặt trước/hông sẽ mất mạng
            GameObject bodyColObj = new GameObject("TrainBodyCollider");
            bodyColObj.tag = "Obstacle";
            if (obsLayer != -1) bodyColObj.layer = obsLayer;
            bodyColObj.transform.SetParent(root.transform, false);

            BoxCollider bodyBox = bodyColObj.AddComponent<BoxCollider>();
            bodyBox.center = new Vector3(0f, 1.15f, 0f);
            bodyBox.size = new Vector3(2.08f, 2.20f, 11.8f);

            Obstacle obs = bodyColObj.AddComponent<Obstacle>();
            obs.Configure(isMovingTrain ? ObstacleType.MovingTrain : ObstacleType.Train, 1);

            // 2. Sàn nóc tàu (RoofFloor tại Y = 2.60m, thuộc layer Ground) -> Cho phép người chơi chạy & nhảy trên nóc tàu!
            GameObject roofFloorObj = new GameObject("RoofFloor");
            if (groundLayer != -1) roofFloorObj.layer = groundLayer;
            roofFloorObj.transform.SetParent(root.transform, false);

            BoxCollider roofBox = roofFloorObj.AddComponent<BoxCollider>();
            roofBox.center = new Vector3(0f, 2.50f, 0f);
            roofBox.size = new Vector3(2.18f, 0.20f, 12.0f);

            // 3. Các khối hình ảnh thân toa tàu (dài 12m, cao 2.55m, rộng 2.15m)
            CreateVisualPart(PrimitiveType.Cube, "MainHull", root.transform,
                new Vector3(0f, 1.28f, 0f), new Vector3(2.14f, 2.45f, 12.0f), bodyMat);

            CreateVisualPart(PrimitiveType.Cube, "RoofDeck", root.transform,
                new Vector3(0f, 2.55f, 0f), new Vector3(2.04f, 0.12f, 11.9f), mats["TrainRoof"]);

            // Dải sọc ngang thân tàu
            Material stripeMat = isMovingTrain ? mats["Neon_Yellow"] : mats["TrainStripe"];
            CreateVisualPart(PrimitiveType.Cube, "SideStripe", root.transform,
                new Vector3(0f, 1.15f, 0f), new Vector3(2.18f, 0.26f, 12.02f), stripeMat);

            // Kính chắn gió đầu tàu & đuôi tàu
            CreateVisualPart(PrimitiveType.Cube, "FrontWindshield", root.transform,
                new Vector3(0f, 1.85f, -5.96f), new Vector3(1.78f, 0.72f, 0.16f), mats["TrainWindow"]);
            CreateVisualPart(PrimitiveType.Cube, "RearWindshield", root.transform,
                new Vector3(0f, 1.85f, 5.96f), new Vector3(1.78f, 0.72f, 0.16f), mats["TrainWindow"]);

            // Đèn pha LED phát sáng phía trước
            CreateVisualPart(PrimitiveType.Sphere, "Headlight_Left", root.transform,
                new Vector3(-0.68f, 0.78f, -6.02f), new Vector3(0.32f, 0.24f, 0.16f), mats["TrainHeadlight"]);
            CreateVisualPart(PrimitiveType.Sphere, "Headlight_Right", root.transform,
                new Vector3(0.68f, 0.78f, -6.02f), new Vector3(0.32f, 0.24f, 0.16f), mats["TrainHeadlight"]);

            // Biển hiệu tuyến tàu phát sáng trên trán tàu
            Material destMat = isMovingTrain ? mats["Neon_Pink"] : mats["Neon_Cyan"];
            CreateVisualPart(PrimitiveType.Cube, "DestinationSign", root.transform,
                new Vector3(0f, 2.34f, -6.01f), new Vector3(1.1f, 0.16f, 0.12f), destMat);

            // Cửa sổ hành khách dọc hai bên hông tàu
            for (int z = -4; z <= 4; z += 2)
            {
                CreateVisualPart(PrimitiveType.Cube, $"Window_{z}", root.transform,
                    new Vector3(0f, 1.80f, z * 1.1f), new Vector3(2.19f, 0.58f, 1.45f), mats["TrainWindow"]);
            }

            if (isMovingTrain)
            {
                MovingTrain mover = root.AddComponent<MovingTrain>();
                mover.Configure(14.0f, 55.0f);
            }

            string path = $"Assets/Prefabs/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Dốc gỗ dẫn lên nóc tàu (Train Ramp - thuộc layer Ground).
        /// </summary>
        private static GameObject BuildTrainRampPrefab(Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject("Train_Ramp");
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1) root.layer = groundLayer;

            // Mặt phẳng nghiêng nối từ mặt đất (Y = 0, Z = -2.8) lên đỉnh tàu (Y = 2.60, Z = +2.8)
            GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "RampSlope";
            if (groundLayer != -1) slope.layer = groundLayer;
            slope.transform.SetParent(root.transform, false);
            slope.transform.localPosition = new Vector3(0f, 1.28f, 0f);
            slope.transform.localRotation = Quaternion.Euler(-24.8f, 0f, 0f);
            slope.transform.localScale = new Vector3(2.12f, 0.22f, 6.25f);
            slope.GetComponent<MeshRenderer>().sharedMaterial = mats["Ramp_Wood"];

            // Viền phát sáng hai bên thành dốc để người chơi dễ nhận biết từ xa
            CreateVisualPart(PrimitiveType.Cube, "RampNeonLeft", slope.transform,
                new Vector3(-0.46f, 0.6f, 0f), new Vector3(0.06f, 0.4f, 0.98f), mats["Neon_Yellow"]);
            CreateVisualPart(PrimitiveType.Cube, "RampNeonRight", slope.transform,
                new Vector3(0.46f, 0.6f, 0f), new Vector3(0.06f, 0.4f, 0.98f), mats["Neon_Yellow"]);

            string path = "Assets/Prefabs/Train_Ramp.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildObstaclePit(Material matPit)
        {
            GameObject root = new GameObject("Obstacle_Pit");
            root.tag = "Pit";

            GameObject triggerObj = new GameObject("PitCollider");
            triggerObj.tag = "Pit";
            triggerObj.transform.SetParent(root.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, -1.6f, 0f);

            BoxCollider box = triggerObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8.5f, 2.0f, 5.2f);

            Obstacle obs = triggerObj.AddComponent<Obstacle>();
            obs.Configure(ObstacleType.Pit, 99);

            CreateVisualPart(PrimitiveType.Cube, "AbyssFloor", root.transform,
                new Vector3(0f, -4.5f, 0f), new Vector3(9.0f, 0.2f, 5.8f), matPit);

            string path = "Assets/Prefabs/Obstacle_Pit.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildObstacleBoulder(Dictionary<string, Material> mats)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Obstacle_Boulder";
            go.tag = "Obstacle";

            int obsLayer = LayerMask.NameToLayer("Obstacle");
            if (obsLayer != -1) go.layer = obsLayer;

            go.transform.localScale = Vector3.one * 1.9f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mats["Obstacle_Boulder"];

            CreateVisualPart(PrimitiveType.Cylinder, "NeonCoreRing", go.transform,
                Vector3.zero, new Vector3(1.06f, 0.12f, 1.06f), mats["Neon_Pink"]);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            RollingBoulder boulder = go.AddComponent<RollingBoulder>();
            boulder.Configure(2.5f, 2.5f, 0.95f, 38.0f);

            Obstacle obs = go.GetComponent<Obstacle>();
            if (obs == null)
            {
                obs = go.AddComponent<Obstacle>();
            }
            obs.Configure(ObstacleType.RollingBoulder, 1);

            string path = "Assets/Prefabs/Obstacle_Boulder.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        #region Dựng Đoạn Đường Sắt & Phố Thị Tokyo Neon (Track Segments)

        private static GameObject CreateTrackSegmentBase(
            string name,
            Dictionary<string, Material> mats,
            bool hasPit = false,
            float pitStart = 12f,
            float pitLength = 5.5f)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<TrackSegment>();

            int groundLayer = LayerMask.NameToLayer("Ground");

            // 1. Nền đá dăm đường sắt (RoadFloor)
            if (!hasPit)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "RoadFloor";
                if (groundLayer != -1) floor.layer = groundLayer;
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.2f, 15f);
                floor.transform.localScale = new Vector3(9.0f, 0.4f, 30.0f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = mats["Ballast"];
            }
            else
            {
                float half1Len = pitStart;
                GameObject floor1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor1.name = "RoadFloor_Part1";
                if (groundLayer != -1) floor1.layer = groundLayer;
                floor1.transform.SetParent(root.transform, false);
                floor1.transform.localPosition = new Vector3(0f, -0.2f, half1Len * 0.5f);
                floor1.transform.localScale = new Vector3(9.0f, 0.4f, half1Len);
                floor1.GetComponent<MeshRenderer>().sharedMaterial = mats["Ballast"];

                float half2Start = pitStart + pitLength;
                float half2Len = 30.0f - half2Start;
                GameObject floor2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor2.name = "RoadFloor_Part2";
                if (groundLayer != -1) floor2.layer = groundLayer;
                floor2.transform.SetParent(root.transform, false);
                floor2.transform.localPosition = new Vector3(0f, -0.2f, half2Start + (half2Len * 0.5f));
                floor2.transform.localScale = new Vector3(9.0f, 0.4f, half2Len);
                floor2.GetComponent<MeshRenderer>().sharedMaterial = mats["Ballast"];
            }

            // 2. Hệ thống 3 làn đường ray (X = -2.5, 0, +2.5): Tà vẹt gỗ + 2 thanh ray thép mỗi làn
            GameObject railsContainer = new GameObject("RailwayTracks");
            railsContainer.transform.SetParent(root.transform, false);

            float[] lanes = new float[] { -2.5f, 0f, 2.5f };
            for (int l = 0; l < lanes.Length; l++)
            {
                float lx = lanes[l];

                // Hai thanh ray thép song song lệch tâm làn ±0.45m
                CreateVisualPart(PrimitiveType.Cube, $"Rail_L_{l}", railsContainer.transform,
                    new Vector3(lx - 0.45f, 0.06f, 15f), new Vector3(0.10f, 0.12f, 30f), mats["SteelRail"]);
                CreateVisualPart(PrimitiveType.Cube, $"Rail_R_{l}", railsContainer.transform,
                    new Vector3(lx + 0.45f, 0.06f, 15f), new Vector3(0.10f, 0.12f, 30f), mats["SteelRail"]);
            }

            // Gộp tà vẹt gỗ ngang qua 3 làn mỗi 3.0m để giảm 75% số lượng GameObject & Draw Call
            for (float z = 1.5f; z < 30.0f; z += 3.0f)
            {
                if (hasPit && z >= pitStart && z <= pitStart + pitLength) continue;

                CreateVisualPart(PrimitiveType.Cube, $"Tie_{z}", railsContainer.transform,
                    new Vector3(0f, 0.01f, z), new Vector3(7.2f, 0.06f, 0.34f), mats["WoodTie"]);
            }

            // 3. Tường gạch chắn hai bên hành lang (X = ±4.4m) kèm dải đèn Neon
            CreateVisualPart(PrimitiveType.Cube, "BrickWall_Left", root.transform,
                new Vector3(-4.4f, 0.55f, 15f), new Vector3(0.35f, 1.1f, 30f), mats["BrickWall"]);
            CreateVisualPart(PrimitiveType.Cube, "BrickWall_Right", root.transform,
                new Vector3(4.4f, 0.55f, 15f), new Vector3(0.35f, 1.1f, 30f), mats["BrickWall"]);

            CreateVisualPart(PrimitiveType.Cube, "WallNeon_Left", root.transform,
                new Vector3(-4.22f, 1.05f, 15f), new Vector3(0.06f, 0.08f, 30f), mats["Neon_Cyan"]);
            CreateVisualPart(PrimitiveType.Cube, "WallNeon_Right", root.transform,
                new Vector3(4.22f, 1.05f, 15f), new Vector3(0.06f, 0.08f, 30f), mats["Neon_Pink"]);

            // 4. Cột điện đường sắt & Cổng vòm cao thế tại Z = 15m
            CreateVisualPart(PrimitiveType.Cube, "CatenaryPole_L", root.transform,
                new Vector3(-4.5f, 3.4f, 15f), new Vector3(0.22f, 6.8f, 0.22f), mats["Barrier_Metal"]);
            CreateVisualPart(PrimitiveType.Cube, "CatenaryPole_R", root.transform,
                new Vector3(4.5f, 3.4f, 15f), new Vector3(0.22f, 6.8f, 0.22f), mats["Barrier_Metal"]);
            CreateVisualPart(PrimitiveType.Cube, "OverheadGantryBeam", root.transform,
                new Vector3(0f, 6.5f, 15f), new Vector3(9.2f, 0.22f, 0.22f), mats["Barrier_Metal"]);

            // 5. Dãy nhà phố Tokyo hai bên đường chạy (X = ±7.6m) kèm cửa sổ & bảng hiệu Neon
            BuildTokyoSideBuildings(root.transform, mats, name.GetHashCode());

            return root;
        }

        private static void BuildTokyoSideBuildings(Transform segmentRoot, Dictionary<string, Material> mats, int seed)
        {
            GameObject scenery = new GameObject("CityScenery");
            scenery.transform.SetParent(segmentRoot, false);

            Material[] bldMats = new Material[]
            {
                mats["Building_1"], mats["Building_2"], mats["Building_3"], mats["Building_4"]
            };
            Material[] neonMats = new Material[]
            {
                mats["Neon_Pink"], mats["Neon_Cyan"], mats["Neon_Yellow"], mats["Neon_Purple"]
            };

            float[] zPositions = new float[] { 6.0f, 16.0f, 25.0f };
            float[] heightsL = new float[] { 9.5f, 13.0f, 10.5f };
            float[] heightsR = new float[] { 12.0f, 8.5f, 13.5f };

            for (int i = 0; i < zPositions.Length; i++)
            {
                float z = zPositions[i];
                int idxL = Mathf.Abs(seed + i) % bldMats.Length;
                int idxR = Mathf.Abs(seed + i + 2) % bldMats.Length;

                // Tòa nhà bên trái
                float hL = heightsL[i];
                CreateVisualPart(PrimitiveType.Cube, $"Bld_L_{i}", scenery.transform,
                    new Vector3(-7.6f, hL * 0.5f, z), new Vector3(4.2f, hL, 8.2f), bldMats[idxL]);

                // Bảng hiệu Neon dọc trên tòa nhà trái
                CreateVisualPart(PrimitiveType.Cube, $"NeonSign_L_{i}", scenery.transform,
                    new Vector3(-5.35f, 4.2f, z), new Vector3(0.12f, 3.2f, 1.1f), neonMats[idxL]);
                CreateVisualPart(PrimitiveType.Cube, $"WindowStrip_L_{i}", scenery.transform,
                    new Vector3(-5.45f, hL - 2.2f, z), new Vector3(0.08f, 0.8f, 5.5f), mats["Window_Lit"]);

                // Tòa nhà bên phải
                float hR = heightsR[i];
                CreateVisualPart(PrimitiveType.Cube, $"Bld_R_{i}", scenery.transform,
                    new Vector3(7.6f, hR * 0.5f, z), new Vector3(4.2f, hR, 8.2f), bldMats[idxR]);

                // Bảng hiệu Neon trên tòa nhà phải
                CreateVisualPart(PrimitiveType.Cube, $"NeonSign_R_{i}", scenery.transform,
                    new Vector3(5.35f, 4.5f, z), new Vector3(0.12f, 3.4f, 1.1f), neonMats[idxR]);
                CreateVisualPart(PrimitiveType.Cube, $"WindowStrip_R_{i}", scenery.transform,
                    new Vector3(5.45f, hR - 2.2f, z), new Vector3(0.08f, 0.8f, 5.5f), mats["Window_Lit"]);
            }
        }

        private static GameObject SpawnItem(GameObject prefab, Transform parent, Vector3 localPos)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.localPosition = localPos;
            return inst;
        }

        /// <summary>
        /// Xếp hàng xu vàng theo đường cong Parabol y = y0 + 4 * peakHeight * t * (1 - t) giống Subway Surfers.
        /// </summary>
        private static void SpawnCoinArc(
            GameObject coinPrefab,
            Transform parent,
            float laneX,
            float startZ,
            float lengthZ,
            float baseHeight = 0.65f,
            float peakHeight = 1.65f,
            int count = 6)
        {
            if (count <= 1) return;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float z = startZ + (t * lengthZ);
                float y = baseHeight + (4.0f * peakHeight * t * (1.0f - t));
                SpawnItem(coinPrefab, parent, new Vector3(laneX, y, z));
            }
        }

        private static TrackSegment SaveTrackPrefab(GameObject root, string filename, float length, int diff, bool heavy, bool safe)
        {
            TrackSegment seg = root.GetComponent<TrackSegment>();
            seg.SetProperties(length, diff, heavy, safe);

            string path = $"Assets/Prefabs/{filename}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<TrackSegment>();
        }

        // === 0. Đoạn xuất phát an toàn (Safe Segment) ===
        private static TrackSegment BuildTrackSegmentSafe(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_Safe", mats);
            for (int z = 6; z <= 24; z += 3)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(0f, 0.65f, z));
            }
            return SaveTrackPrefab(root, "TrackSegment_Safe", 30f, 1, false, true);
        }

        // === Stage 1 - 1: Rào chắn Đỏ-Trắng & Vòng cung Xu nhảy ===
        private static TrackSegment BuildTrackSegmentS1_BarriersAndArcs(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S1_1", mats);
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(0f, 0f, 12f));
            SpawnCoinArc(m.coinPrefab, root.transform, 0f, 8.5f, 7.0f, 0.65f, 1.55f, 6);

            SpawnItem(m.obstacleOverheadPrefab, root.transform, new Vector3(-2.5f, 0f, 22f));
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(2.5f, 0f, 22f));
            SpawnCoinArc(m.coinPrefab, root.transform, 2.5f, 18.5f, 7.0f, 0.65f, 1.55f, 6);

            return SaveTrackPrefab(root, "TrackSegment_S1_1", 30f, 1, false, false);
        }

        // === Stage 1 - 2: Toa tàu đứng yên kèm Dốc leo lên nóc tàu & Hàng xu trên nóc ===
        private static TrackSegment BuildTrackSegmentS1_SingleTrainWithRamp(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S1_2", mats);

            // Dốc ở Z = 8.5m nối thẳng vào đầu toa tàu ở Z = 17.5m (toa dài 12m từ 11.5m -> 23.5m)
            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(0f, 0f, 8.6f));
            SpawnItem(m.trainBluePrefab, root.transform, new Vector3(0f, 0f, 17.5f));

            // Xu dẫn từ dốc lên thẳng nóc tàu (Y = 3.25m)
            for (int z = 12; z <= 22; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(0f, 3.25f, z));
            }
            SpawnItem(m.crystalPrefab, root.transform, new Vector3(0f, 3.45f, 23f));

            // Vật cản nhẹ ở làn phụ
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(-2.5f, 0f, 16f));

            return SaveTrackPrefab(root, "TrackSegment_S1_2", 30f, 1, false, false);
        }

        // === Stage 1 - 3: Tàu bên phải + Rào chắn hai làn ===
        private static TrackSegment BuildTrackSegmentS1_SideTrainAndBarriers(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S1_3", mats);

            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(2.5f, 0f, 7.6f));
            SpawnItem(m.trainYellowPrefab, root.transform, new Vector3(2.5f, 0f, 16.5f));
            SpawnItem(m.shieldPowerUpPrefab, root.transform, new Vector3(2.5f, 3.35f, 18f));

            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(-2.5f, 0f, 12f));
            SpawnItem(m.obstacleOverheadPrefab, root.transform, new Vector3(0f, 0f, 21f));

            for (int z = 16; z <= 24; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(0f, 0.65f, z));
            }

            return SaveTrackPrefab(root, "TrackSegment_S1_3", 30f, 1, true, false);
        }

        // === Stage 2 - 1: Hai toa tàu song song - Nhảy Parkour từ nóc tàu này sang nóc tàu kia ===
        private static TrackSegment BuildTrackSegmentS2_DualTrainsRooftopJump(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S2_1", mats);

            // Làn trái: Dốc + Toa tàu thứ nhất
            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(-2.5f, 0f, 6.6f));
            SpawnItem(m.trainBluePrefab, root.transform, new Vector3(-2.5f, 0f, 15.5f));

            // Làn phải: Toa tàu thứ hai lệch về sau để nhảy chuyển làn trên không trung
            SpawnItem(m.trainYellowPrefab, root.transform, new Vector3(0f, 0f, 20.5f));

            for (int z = 10; z <= 16; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(-2.5f, 3.25f, z));
            }
            for (int z = 18; z <= 26; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(0f, 3.25f, z));
            }

            SpawnItem(m.crystalPrefab, root.transform, new Vector3(0f, 3.4f, 26f));
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(2.5f, 0f, 15f));

            return SaveTrackPrefab(root, "TrackSegment_S2_1", 30f, 2, true, false);
        }

        // === Stage 2 - 2: Hố sụp đường ray + Rào chắn trượt cao ===
        private static TrackSegment BuildTrackSegmentS2_PitAndRollBarriers(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S2_2", mats, hasPit: true, pitStart: 12f, pitLength: 5.2f);
            SpawnItem(m.obstaclePitPrefab, root.transform, new Vector3(0f, 0f, 14.6f));
            SpawnCoinArc(m.coinPrefab, root.transform, 0f, 10.5f, 8.2f, 0.8f, 1.9f, 7);

            SpawnItem(m.obstacleOverheadPrefab, root.transform, new Vector3(0f, 0f, 24f));
            SpawnItem(m.magnetPowerUpPrefab, root.transform, new Vector3(-2.5f, 0.85f, 24f));

            return SaveTrackPrefab(root, "TrackSegment_S2_2", 30f, 2, true, false);
        }

        // === Stage 2 - 3: Vượt Rào chắn ngay trên Nóc Tàu ===
        private static TrackSegment BuildTrackSegmentS2_RooftopObstacles(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S2_3", mats);

            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(0f, 0f, 6.6f));
            SpawnItem(m.trainBluePrefab, root.transform, new Vector3(0f, 0f, 15.5f));

            // Đặt 1 rào chắn thấp ngay trên nóc toa tàu (Y = 2.60m)
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(0f, 2.60f, 16.0f));
            SpawnCoinArc(m.coinPrefab, root.transform, 0f, 12.5f, 7.0f, 3.25f, 1.45f, 6);

            SpawnItem(m.obstacleOverheadPrefab, root.transform, new Vector3(-2.5f, 0f, 14f));
            SpawnItem(m.obstacleLowPrefab, root.transform, new Vector3(2.5f, 0f, 22f));

            return SaveTrackPrefab(root, "TrackSegment_S2_3", 30f, 2, false, false);
        }

        // === Stage 3 - 1: Đoàn tàu Tokyo Express lao ngược chiều (MovingTrain) ===
        private static TrackSegment BuildTrackSegmentS3_OncomingExpress(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S3_1", mats);

            // Đoàn tàu đỏ chạy ngược chiều ở làn giữa
            SpawnItem(m.movingTrainRedPrefab, root.transform, new Vector3(0f, 0f, 23.0f));

            // Làn trái có dốc lên toa tàu đứng yên để né tàu đang lao tới
            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(-2.5f, 0f, 7.0f));
            SpawnItem(m.trainYellowPrefab, root.transform, new Vector3(-2.5f, 0f, 16.0f));

            for (int z = 11; z <= 21; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(-2.5f, 3.25f, z));
            }
            SpawnItem(m.crystalPrefab, root.transform, new Vector3(2.5f, 0.85f, 20f));

            return SaveTrackPrefab(root, "TrackSegment_S3_1", 30f, 3, true, false);
        }

        // === Stage 3 - 2: Hai đoàn tàu di chuyển so le + Nhân đôi điểm ===
        private static TrackSegment BuildTrackSegmentS3_DualMovingTrains(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S3_2", mats);

            SpawnItem(m.movingTrainRedPrefab, root.transform, new Vector3(-2.5f, 0f, 18.0f));
            SpawnItem(m.movingTrainRedPrefab, root.transform, new Vector3(2.5f, 0f, 26.0f));

            SpawnItem(m.obstacleOverheadPrefab, root.transform, new Vector3(0f, 0f, 12.0f));
            SpawnItem(m.doubleScorePowerUpPrefab, root.transform, new Vector3(0f, 0.85f, 18.0f));
            SpawnCoinArc(m.coinPrefab, root.transform, 0f, 20.0f, 7.0f, 0.65f, 1.55f, 6);

            return SaveTrackPrefab(root, "TrackSegment_S3_2", 30f, 3, true, false);
        }

        // === Stage 3 - 3: Shibuya Rush Hour Gauntlet (Tàu chạy + Drone lăn ngang + Tàu có dốc) ===
        private static TrackSegment BuildTrackSegmentS3_TokyoRushGauntlet(PrefabManifest m, Dictionary<string, Material> mats)
        {
            GameObject root = CreateTrackSegmentBase("TrackSegment_S3_3", mats);

            SpawnItem(m.trainRampPrefab, root.transform, new Vector3(2.5f, 0f, 6.6f));
            SpawnItem(m.trainBluePrefab, root.transform, new Vector3(2.5f, 0f, 15.5f));
            SpawnItem(m.movingTrainRedPrefab, root.transform, new Vector3(-2.5f, 0f, 22.0f));
            SpawnItem(m.obstacleBoulderPrefab, root.transform, new Vector3(0f, 0.95f, 25.0f));

            SpawnItem(m.crystalPrefab, root.transform, new Vector3(2.5f, 3.45f, 16.0f));
            for (int z = 10; z <= 20; z += 2)
            {
                SpawnItem(m.coinPrefab, root.transform, new Vector3(2.5f, 3.25f, z));
            }

            return SaveTrackPrefab(root, "TrackSegment_S3_3", 30f, 3, true, false);
        }

        #endregion

        #endregion

        #region 5. Scene Setup (Main.unity - Subway Surfers Tokyo Atmosphere)

        private static void SetupScene(GameConfig config, PrefabManifest manifest)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Ánh sáng & Bầu trời Vibrant Subway Surfers Tokyo
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = HexColor("#8FD3FF");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = HexColor("#7EC8FF");
            RenderSettings.ambientEquatorColor = HexColor("#FFE8C2");
            RenderSettings.ambientGroundColor = HexColor("#5A5268");

            // 2. Directional Light nắng ấm rực rỡ
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = HexColor("#FFF4D6");
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            // 3. Player
            GameObject playerGo = (GameObject)PrefabUtility.InstantiatePrefab(manifest.playerPrefab);
            playerGo.name = "Player";
            playerGo.transform.position = new Vector3(0f, 0.05f, 0f);

            PlayerController pController = playerGo.GetComponent<PlayerController>();
            PlayerHealth pHealth = playerGo.GetComponent<PlayerHealth>();

            // 4. Main Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexColor("#1B2845");
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();

            CameraFollow camFollow = camGo.AddComponent<CameraFollow>();
            camFollow.SetTarget(playerGo.transform);
            camFollow.SetOffset(new Vector3(0f, 4.6f, -7.2f));

            // 5. Track Spawner
            GameObject spawnerGo = new GameObject("TrackSpawner");
            TrackSpawner spawner = spawnerGo.AddComponent<TrackSpawner>();
            spawner.SetConfig(config);
            spawner.SetPrefabs(
                manifest.safeSegments,
                manifest.stage1Segments,
                manifest.stage2Segments,
                manifest.stage3Segments
            );

            // 6. Managers
            GameObject managersGo = new GameObject("Managers");
            GameManager gameMgr = managersGo.AddComponent<GameManager>();
            ScoreManager scoreMgr = managersGo.AddComponent<ScoreManager>();
            PowerUpManager powerUpMgr = managersGo.AddComponent<PowerUpManager>();

            // 7. EventSystem
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            // 8. Canvas & UIManager (Phong cách Arcade Subway Surfers)
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            UIManager uiManager = canvasGo.AddComponent<UIManager>();
            BuildUIHierarchy(canvasGo.transform, uiManager);

            // Liên kết tham chiếu cho các Manager
            Transform shieldVisual = playerGo.transform.Find("ShieldVisual");
            powerUpMgr.SetReferences(playerGo.transform, pHealth, shieldVisual != null ? shieldVisual.gameObject : null, config);
            scoreMgr.SetPlayerController(pController);
            gameMgr.SetReferences(pController, pHealth, spawner, scoreMgr, powerUpMgr, config);

            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(camFollow);
            EditorUtility.SetDirty(powerUpMgr);
            EditorUtility.SetDirty(scoreMgr);
            EditorUtility.SetDirty(gameMgr);
            EditorUtility.SetDirty(uiManager);
            EditorUtility.SetDirty(pController);
            EditorUtility.SetDirty(pHealth);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        }

        private static void BuildUIHierarchy(Transform canvasRoot, UIManager uiManager)
        {
            // === 1. Main Menu Panel (Subway Surfers Arcade Style) ===
            RectTransform menuPanel = CreateUIPanel(canvasRoot, "MainMenuPanel", new Color(0.04f, 0.06f, 0.14f, 0.72f));

            // Khung Hero Card trung tâm có viền Vàng - Hồng Neon
            RectTransform heroCard = CreateStyledBadge(menuPanel, "HeroCard",
                new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(780, 520),
                new Color(0.06f, 0.10f, 0.22f, 0.94f), HexColor("#FFD700"), 6f);

            RectTransform tourBadge = CreateStyledBadge(heroCard, "WorldTourBadge",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(460, 48),
                HexColor("#FF2A85"), HexColor("#FFF4D6"), 3f);
            CreateUIText(tourBadge, "SubTitle", "WORLD TOUR : TOKYO NIGHT NEON", 22f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white);

            // Chữ bóng đổ (Drop Shadow) + Chữ chính kiểu Subway Surfers
            CreateUIText(heroCard, "TitleShadow", "SUBWAY SURFERS\nCRYSTAL RUN", 52f, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(4, -4), new Vector2(720, 140), TextAlignmentOptions.Center, HexColor("#120824"));
            CreateUIText(heroCard, "TitleText", "SUBWAY SURFERS\nCRYSTAL RUN", 52f, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(720, 140), TextAlignmentOptions.Center, HexColor("#FFD700"));

            // Huy hiệu Kỷ lục (TOP RUN Badge)
            RectTransform recordBadge = CreateStyledBadge(heroCard, "RecordBadge",
                new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(420, 62),
                new Color(0.03f, 0.06f, 0.15f, 0.95f), HexColor("#00F0FF"), 3f);
            TMP_Text menuHighScore = CreateUIText(recordBadge, "HighScoreText", "KỶ LỤC: 0", 28f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, HexColor("#00F0FF"));

            CreateUIText(heroCard, "ControlsHint", "VUỐT MÀN HÌNH HOẶC PHÍM A/D/W/S ĐỂ NÉ TÀU & SĂN XU", 18f, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(680, 36), TextAlignmentOptions.Center, HexColor("#FFE8C2"));

            Button startBtn = CreateArcadeButton(heroCard, "StartButton", "CHẠM ĐỂ CHẠY NGAY!", new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), new Vector2(-115, 0), new Vector2(360, 74), HexColor("#00C853"), HexColor("#FFD700"), Color.white, 26f);
            Button quitBtn = CreateArcadeButton(heroCard, "QuitButton", "THOÁT", new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), new Vector2(205, 0), new Vector2(210, 74), HexColor("#D9383A"), HexColor("#FFF4D6"), Color.white, 24f);
            uiManager.ConfigureMainMenu(menuHighScore, startBtn, quitBtn);

            // === 2. HUD Panel (Chuẩn bố cục Subway Surfers) ===
            RectTransform hud = CreateUIPanel(canvasRoot, "HUDPanel", Color.clear);
            hud.GetComponent<Image>().raycastTarget = false;

            // Nút Tạm dừng góc trên bên trái (kiểu Subway Surfers Blue Square Button)
            Button pauseBtn = CreateArcadeButton(hud, "PauseButton", "||", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56, -54), new Vector2(68, 68), HexColor("#1D63B8"), HexColor("#00F0FF"), Color.white, 28f);

            // Cụm Điểm số & Quãng đường góc trên bên phải (6 chữ số 000000 giống Subway Surfers)
            RectTransform scoreBadge = CreateStyledBadge(hud, "ScoreBadge",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -52f), new Vector2(330f, 68f),
                new Color(0.05f, 0.09f, 0.22f, 0.90f), HexColor("#FFD700"), 3f);
            CreateUIText(scoreBadge, "StarTag", "ĐIỂM", 18f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46, 0), new Vector2(70, 40), TextAlignmentOptions.Left, HexColor("#00F0FF"));
            TMP_Text scoreText = CreateUIText(scoreBadge, "ScoreText", "000000", 38f, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-120, 0), new Vector2(210, 54), TextAlignmentOptions.Right, HexColor("#FFD700"));

            RectTransform distBadge = CreateStyledBadge(hud, "DistBadge",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-125f, -110f), new Vector2(200f, 40f),
                new Color(0.05f, 0.09f, 0.22f, 0.85f), HexColor("#00F0FF"), 2f);
            TMP_Text distText = CreateUIText(distBadge, "DistanceText", "0 m", 22f, Vector2.zero, Vector2.one, new Vector2(-10, 0), Vector2.zero, TextAlignmentOptions.Right, HexColor("#8FD3FF"));

            // Cụm Xu Vàng, Tinh Thể & Mạng bên trái (dưới nút Pause)
            RectTransform coinPill = CreateStyledBadge(hud, "CoinPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(138f, -118f), new Vector2(230f, 46f),
                new Color(0.05f, 0.09f, 0.22f, 0.88f), HexColor("#FFD700"), 3f);
            TMP_Text coinsText = CreateUIText(coinPill, "CoinsText", "XU: 0", 24f, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-24, 0), TextAlignmentOptions.Left, HexColor("#FFD700"));

            RectTransform crystalPill = CreateStyledBadge(hud, "CrystalPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(138f, -172f), new Vector2(230f, 44f),
                new Color(0.05f, 0.09f, 0.22f, 0.88f), HexColor("#00F0FF"), 2f);
            TMP_Text crystalsText = CreateUIText(crystalPill, "CrystalsText", "TINH THỂ: 0/10", 21f, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-24, 0), TextAlignmentOptions.Left, HexColor("#00F0FF"));

            RectTransform livesPill = CreateStyledBadge(hud, "LivesPill",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(138f, -224f), new Vector2(230f, 44f),
                new Color(0.05f, 0.09f, 0.22f, 0.88f), HexColor("#FF2A85"), 2f);
            TMP_Text livesText = CreateUIText(livesPill, "LivesText", "MẠNG: 3", 22f, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-24, 0), TextAlignmentOptions.Left, HexColor("#FF4D6D"));

            // Thanh đếm ngược Power-Up (Hoverboard / Jetpack / Sneaker) dưới đáy màn hình
            RectTransform buffContainer = CreateUIPanel(hud, "BuffContainer", Color.clear);
            buffContainer.GetComponent<Image>().raycastTarget = false;
            buffContainer.anchorMin = new Vector2(0.5f, 0f);
            buffContainer.anchorMax = new Vector2(0.5f, 0f);
            buffContainer.anchoredPosition = new Vector2(0f, 56f);
            buffContainer.sizeDelta = new Vector2(820, 54);

            RectTransform shieldBox = CreateStyledBadge(buffContainer, "ShieldBuff",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-245, 0), new Vector2(225, 48),
                HexColor("#1D63B8"), HexColor("#00F0FF"), 3f);
            TMP_Text shieldTxt = CreateUIText(shieldBox, "Txt", "VÁN TRƯỢT: 8s", 20f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white);

            RectTransform magnetBox = CreateStyledBadge(buffContainer, "MagnetBuff",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(225, 48),
                HexColor("#E62E2D"), HexColor("#FFD700"), 3f);
            TMP_Text magnetTxt = CreateUIText(magnetBox, "Txt", "NAM CHÂM: 8s", 20f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white);

            RectTransform doubleBox = CreateStyledBadge(buffContainer, "DoubleScoreBuff",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(245, 0), new Vector2(225, 48),
                HexColor("#8E2DE2"), HexColor("#FF2A85"), 3f);
            TMP_Text doubleTxt = CreateUIText(doubleBox, "Txt", "X2 SAO: 10s", 20f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white);

            uiManager.ConfigureHUD(scoreText, distText, coinsText, livesText, crystalsText, pauseBtn,
                shieldBox.gameObject, shieldTxt, magnetBox.gameObject, magnetTxt, doubleBox.gameObject, doubleTxt);

            // === 3. Pause Panel ===
            RectTransform pause = CreateUIPanel(canvasRoot, "PausePanel", new Color(0.03f, 0.05f, 0.12f, 0.84f));
            RectTransform pauseCard = CreateStyledBadge(pause, "PauseCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 430),
                new Color(0.06f, 0.11f, 0.24f, 0.96f), HexColor("#00F0FF"), 5f);

            CreateUIText(pauseCard, "PauseTitle", "TẠM DỪNG CUỘC ĐUA", 40f, new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(460, 70), TextAlignmentOptions.Center, HexColor("#FFD700"));
            Button resumeBtn = CreateArcadeButton(pauseCard, "ResumeButton", "TIẾP TỤC CHẠY", new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(320, 66), HexColor("#00C853"), HexColor("#FFD700"), Color.white, 24f);
            Button pauseRestartBtn = CreateArcadeButton(pauseCard, "RestartButton", "CHƠI LẠI TỪ ĐẦU", new Vector2(0.5f, 0.37f), new Vector2(0.5f, 0.37f), Vector2.zero, new Vector2(320, 62), HexColor("#FF7A00"), HexColor("#FFF4D6"), Color.white, 23f);
            Button pauseQuitBtn = CreateArcadeButton(pauseCard, "QuitButton", "THOÁT GAME", new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(320, 60), HexColor("#D9383A"), HexColor("#FFF4D6"), Color.white, 22f);
            uiManager.ConfigurePause(resumeBtn, pauseRestartBtn, pauseQuitBtn);

            // === 4. Game Over Panel (Subway Surfers Scoreboard Card) ===
            RectTransform gameOver = CreateUIPanel(canvasRoot, "GameOverPanel", new Color(0.04f, 0.04f, 0.12f, 0.88f));
            RectTransform overCard = CreateStyledBadge(gameOver, "GameOverCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 540),
                new Color(0.07f, 0.10f, 0.23f, 0.96f), HexColor("#FF2A85"), 5f);

            CreateUIText(overCard, "OverTitle", "BỊ BẮT RỒI!", 48f, new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(600, 75), TextAlignmentOptions.Center, HexColor("#FFD700"));
            GameObject newRecordObj = CreateUIText(overCard, "NewRecordNotice", "KỶ LỤC MỚI TOKYO!", 28f, new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(500, 44), TextAlignmentOptions.Center, HexColor("#00F0FF")).gameObject;

            RectTransform statsBox = CreateStyledBadge(overCard, "StatsBox",
                new Vector2(0.5f, 0.49f), new Vector2(0.5f, 0.49f), Vector2.zero, new Vector2(560, 210),
                new Color(0.03f, 0.06f, 0.15f, 0.92f), HexColor("#1D63B8"), 3f);

            TMP_Text finalScore = CreateUIText(statsBox, "FinalScore", "ĐIỂM SỐ: 0", 34f, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(500, 48), TextAlignmentOptions.Center, HexColor("#FFD700"));
            TMP_Text finalDist = CreateUIText(statsBox, "FinalDistance", "QUÃNG ĐƯỜNG: 0m", 25f, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(500, 40), TextAlignmentOptions.Center, HexColor("#8FD3FF"));
            TMP_Text finalCoins = CreateUIText(statsBox, "FinalCoins", "XU THU THẬP: 0", 25f, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(500, 40), TextAlignmentOptions.Center, HexColor("#FFC845"));
            TMP_Text overHighScore = CreateUIText(statsBox, "GameOverHighScore", "KỶ LỤC: 0", 25f, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(500, 40), TextAlignmentOptions.Center, HexColor("#00F0FF"));

            Button overRestartBtn = CreateArcadeButton(overCard, "RestartButton", "CHẠY LẠI NGAY!", new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(-110, 0), new Vector2(330, 70), HexColor("#00C853"), HexColor("#FFD700"), Color.white, 26f);
            Button overQuitBtn = CreateArcadeButton(overCard, "QuitButton", "THOÁT", new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(185, 0), new Vector2(200, 70), HexColor("#D9383A"), HexColor("#FFF4D6"), Color.white, 23f);
            uiManager.ConfigureGameOver(finalScore, finalDist, finalCoins, overHighScore, newRecordObj, overRestartBtn, overQuitBtn);

            uiManager.ConfigurePanels(menuPanel.gameObject, hud.gameObject, pause.gameObject, gameOver.gameObject);
        }

        private static RectTransform CreateStyledBadge(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color innerColor,
            Color borderColor,
            float borderThickness)
        {
            GameObject borderGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(parent, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = anchorMin;
            borderRt.anchorMax = anchorMax;
            borderRt.anchoredPosition = anchoredPos;
            borderRt.sizeDelta = sizeDelta;
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.color = borderColor;
            borderImg.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(borderGo.transform, false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(borderThickness, borderThickness);
            fillRt.offsetMax = new Vector2(-borderThickness, -borderThickness);
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.color = innerColor;
            fillImg.raycastTarget = false;

            return borderRt;
        }

        private static Button CreateArcadeButton(
            Transform parent,
            string name,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color btnColor,
            Color borderColor,
            Color textColor,
            float fontSize)
        {
            GameObject borderGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            borderGo.transform.SetParent(parent, false);
            RectTransform rt = borderGo.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            borderGo.GetComponent<Image>().color = borderColor;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(borderGo.transform, false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 5f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.color = btnColor;

            Button btn = borderGo.GetComponent<Button>();
            btn.targetGraphic = fillImg;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(fillGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.raycastTarget = false;

            return btn;
        }

        private static RectTransform CreateUIPanel(Transform parent, string name, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = bgColor;
            return rt;
        }

        private static TMP_Text CreateUIText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateUIButton(
            Transform parent,
            string name,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color btnColor,
            Color textColor)
        {
            return CreateArcadeButton(parent, name, text, anchorMin, anchorMax, anchoredPos, sizeDelta, btnColor, Color.white, textColor, 23f);
        }

        #endregion

        #region 6. Build Settings & WebGL Export (Render Ready)

        private static void SetupBuildSettings()
        {
            string scenePath = "Assets/Scenes/Main.unity";
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        [MenuItem("Tools/Crystal Run/Build WebGL (For Render)", priority = 10)]
        public static void BuildWebGLForRender()
        {
            // Luôn dựng lại toàn bộ Prefab, Material, UI & Scene mới nhất trước khi xuất bản WebGL
            BuildAll();
            string scenePath = "Assets/Scenes/Main.unity";

            SetupBuildSettings();

            // Tắt nén Brotli/Gzip mặc định để tương thích 100% với Render Static Site mà không bị lỗi Header
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.runInBackground = true;

            string outputPath = "WebGLBuild";
            EnsureDirectory(outputPath);

            // Sao lưu giao diện Subway Surfers tùy chỉnh trong index.html trước khi Unity ghi đè
            string indexPath = Path.Combine(outputPath, "index.html");
            string customIndexBackup = null;
            if (File.Exists(indexPath))
            {
                string existingHtml = File.ReadAllText(indexPath);
                if (existingHtml.Contains("subway-splash"))
                {
                    customIndexBackup = existingHtml;
                }
            }

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new string[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                // Khôi phục giao diện Subway Surfers & vá loader.js hỗ trợ cảm ứng di động
                if (!string.IsNullOrEmpty(customIndexBackup))
                {
                    File.WriteAllText(indexPath, customIndexBackup);
                }

                string loaderPath = Path.Combine(outputPath, "Build", "WebGLBuild.loader.js");
                if (File.Exists(loaderPath))
                {
                    string loaderCode = File.ReadAllText(loaderPath);
                    loaderCode = loaderCode.Replace("mobile:e(\"iPhone|iPad|iPod|Android\")", "mobile:!1");
                    File.WriteAllText(loaderPath, loaderCode);
                }

                Debug.Log($"[CrystalRunBuilder] Đã xuất bản WebGL thành công tại thư mục '{outputPath}/'!");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Build WebGL Hoàn tất",
                        "Đã xuất bản game sang WebGL tại thư mục 'WebGLBuild/'!\n\n" +
                        "Bây giờ bạn có thể commit và push lên GitHub để Render tự động deploy.",
                        "OK"
                    );
                }
            }
            else
            {
                Debug.LogError($"[CrystalRunBuilder] Build WebGL thất bại: {report.summary.result}");
            }
        }

        #endregion

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
