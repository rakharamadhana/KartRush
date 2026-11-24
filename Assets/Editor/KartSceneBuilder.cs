using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace KartRush.EditorTools
{
    /// <summary>Builds the KartTrack prototype scene: procedural track, walls, kart, camera, HUD, race logic.</summary>
    public static class KartSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/KartTrack.unity";
        const int Samples = 240;
        const float RoadWidth = 16f;
        const float ShoulderWidth = 6f;
        const float WallHeight = 1.6f;
        const float WallThickness = 0.9f;
        const int CheckpointCount = 8;

        static Vector3[] center;
        static Vector3[] tangent;
        static Vector3[] right;

        [MenuItem("KartRush/Build Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Meshes");
            Directory.CreateDirectory("Assets/Textures");
            Directory.CreateDirectory("Assets/Physics");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildTrackData();

            var road = GetMat("Road", new Color(0.17f, 0.17f, 0.19f), 0.15f);
            var grass = GetMat("Grass", new Color(0.28f, 0.58f, 0.22f), 0.05f);
            var wallRed = GetMat("WallRed", new Color(0.85f, 0.12f, 0.12f), 0.3f);
            var wallWhite = GetMat("WallWhite", new Color(0.95f, 0.95f, 0.95f), 0.3f);
            var kartRed = GetMat("KartBody", new Color(0.9f, 0.08f, 0.1f), 0.6f);
            var kartDark = GetMat("KartDark", new Color(0.12f, 0.12f, 0.14f), 0.4f);
            var kartWhite = GetMat("KartWhite", new Color(0.95f, 0.95f, 0.95f), 0.5f);
            var skin = GetMat("Skin", new Color(0.96f, 0.76f, 0.58f), 0.3f);
            var suit = GetMat("Suit", new Color(0.15f, 0.35f, 0.9f), 0.3f);
            var checker = GetCheckerMaterial();

            // Environment
            BuildLighting();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(70f, 1f, 70f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = grass;
            ground.AddComponent<OffRoadSurface>();

            // Track
            var track = new GameObject("Track");
            BuildRoad(track.transform, road);
            BuildWall(track.transform, "WallLeft", -(RoadWidth * 0.5f + ShoulderWidth), -1f, wallRed, wallWhite);
            BuildWall(track.transform, "WallRight", +(RoadWidth * 0.5f + ShoulderWidth), +1f, wallRed, wallWhite);
            BuildStartLine(track.transform, checker, kartDark);

            // Race manager + checkpoints
            var raceGo = new GameObject("RaceManager");
            var race = raceGo.AddComponent<RaceManager>();
            for (int i = 0; i < CheckpointCount; i++)
            {
                int idx = i * Samples / CheckpointCount;
                var go = new GameObject($"Checkpoint_{i}");
                go.transform.SetParent(raceGo.transform);
                go.transform.position = center[idx] + Vector3.up * 3f;
                go.transform.rotation = Quaternion.LookRotation(tangent[idx], Vector3.up);
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3((RoadWidth + ShoulderWidth * 2f), 8f, 3f);
                var cp = go.AddComponent<Checkpoint>();
                cp.index = i;
                cp.race = race;
                race.checkpoints.Add(cp);
            }

            // Items
            var boxOuter = GetMat("ItemBoxOuter", new Color(0.2f, 0.75f, 1f), 0.8f);
            boxOuter.EnableKeyword("_EMISSION");
            boxOuter.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.9f) * 1.5f);
            var boxInner = GetMat("ItemBoxCore", new Color(1f, 0.85f, 0.2f), 0.8f);
            boxInner.EnableKeyword("_EMISSION");
            boxInner.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f) * 2f);
            var itemAssets = raceGo.AddComponent<ItemAssets>();
            itemAssets.bananaMat = GetMat("Banana", new Color(1f, 0.88f, 0.15f), 0.5f);
            itemAssets.bananaDarkMat = GetMat("BananaTip", new Color(0.25f, 0.15f, 0.05f), 0.3f);
            itemAssets.shellMat = GetMat("Shell", new Color(0.1f, 0.75f, 0.25f), 0.7f);
            itemAssets.shellRingMat = GetMat("ShellRing", new Color(0.97f, 0.97f, 0.9f), 0.4f);
            BuildItemBoxes(track.transform, boxOuter, boxInner);

            // Kart
            // Start grid: 2 karts per row, rows 8 m apart; the player starts in the third row.
            const int playerSlot = 4;
            GridSlot(playerSlot, out Vector3 startPos, out float startYaw);
            var kart = BuildKart("Kart_Player", startPos, startYaw, kartRed, kartDark, kartWhite, skin, suit);
            AddRacer(kart, race, "You", true, new Color(0.9f, 0.08f, 0.1f), startPos, startYaw);

            var rivals = new[]
            {
                new Rival("Bolt",   new Color(0.15f, 0.45f, 1.0f), new Color(1.0f, 0.85f, 0.1f), 0.99f, -3.0f),
                new Rival("Dash",   new Color(1.0f, 0.8f, 0.1f),   new Color(0.2f, 0.2f, 0.25f), 0.96f,  3.0f),
                new Rival("Nitro",  new Color(0.15f, 0.75f, 0.3f), new Color(0.95f, 0.95f, 0.95f), 0.93f, -1.5f),
                new Rival("Turbo",  new Color(0.65f, 0.25f, 0.9f), new Color(1.0f, 0.5f, 0.1f),  0.90f,  1.5f),
                new Rival("Zippy",  new Color(1.0f, 0.5f, 0.1f),   new Color(0.15f, 0.35f, 0.9f), 0.87f,  0f),
            };
            int[] rivalSlots = { 0, 1, 2, 3, 5 };
            for (int i = 0; i < rivals.Length; i++)
            {
                var rv = rivals[i];
                GridSlot(rivalSlots[i], out Vector3 p, out float y);
                var body = GetMat("Kart_" + rv.name, rv.body, 0.6f);
                var suitMat = GetMat("Suit_" + rv.name, rv.suit, 0.3f);
                var ai = BuildKart("Kart_" + rv.name, p, y, body, kartDark, kartWhite, skin, suitMat);
                AddRacer(ai, race, rv.name, false, rv.body, p, y);
                var driver = ai.gameObject.AddComponent<KartAutoDriver>();
                driver.skill = rv.skill;
                driver.laneOffset = rv.lane;
            }

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 800f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<MusicPlayer>();
            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var kartCam = camGo.AddComponent<KartCamera>();
            kartCam.kart = kart;
            camGo.transform.position = startPos - Vector3.forward * 8f + Vector3.up * 3f;

            race.kart = kart;
            race.cam = kartCam;
            race.racingLine = (Vector3[])center.Clone();
            race.startPosition = startPos;
            race.startYaw = startYaw;

            BuildPostProcessing();
            BuildHud(race, kart);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[KartRush] Built {ScenePath}. Track length ~{TrackLength():F0} m.");
        }

        // ---------------------------------------------------------------- track data

        static Vector3 Centerline(float t)
        {
            float r = 1f + 0.18f * Mathf.Cos(2f * t) + 0.08f * Mathf.Sin(3f * t);
            return new Vector3(115f * r * Mathf.Cos(t), 0f, 72f * r * Mathf.Sin(t));
        }

        static void BuildTrackData()
        {
            center = new Vector3[Samples];
            tangent = new Vector3[Samples];
            right = new Vector3[Samples];
            for (int i = 0; i < Samples; i++)
                center[i] = Centerline(Mathf.PI * 2f * i / Samples);

            float minRadius = float.MaxValue;
            for (int i = 0; i < Samples; i++)
            {
                Vector3 prev = center[(i - 1 + Samples) % Samples];
                Vector3 next = center[(i + 1) % Samples];
                tangent[i] = (next - prev).normalized;
                right[i] = Vector3.Cross(Vector3.up, tangent[i]).normalized;

                // circumradius of three neighbouring points = local turn radius
                float a = (center[i] - prev).magnitude, b = (next - center[i]).magnitude, c = (next - prev).magnitude;
                float area2 = Mathf.Abs(Vector3.Cross(center[i] - prev, next - prev).y);
                if (area2 > 0.0001f) minRadius = Mathf.Min(minRadius, a * b * c / (2f * area2));
            }
            Debug.Log($"[KartRush] Tightest corner radius on the centerline: {minRadius:F1} m (walls sit {RoadWidth * 0.5f + ShoulderWidth:F0} m from it).");
        }

        static float TrackLength()
        {
            float len = 0f;
            for (int i = 0; i < Samples; i++) len += Vector3.Distance(center[i], center[(i + 1) % Samples]);
            return len;
        }

        // ---------------------------------------------------------------- meshes

        static void BuildRoad(Transform parent, Material mat)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float v = 0f;
            for (int i = 0; i <= Samples; i++)
            {
                int k = i % Samples;
                Vector3 l = center[k] - right[k] * (RoadWidth * 0.5f);
                Vector3 r = center[k] + right[k] * (RoadWidth * 0.5f);
                verts.Add(l); verts.Add(r);
                uvs.Add(new Vector2(0f, v)); uvs.Add(new Vector2(1f, v));
                v += Vector3.Distance(center[k], center[(k + 1) % Samples]) / RoadWidth;
                if (i < Samples)
                {
                    int b = i * 2;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var mesh = new Mesh { name = "RoadMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, "RoadMesh");

            var go = new GameObject("Road");
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void BuildWall(Transform parent, string name, float offset, float outSign, Material a, Material b)
        {
            var verts = new List<Vector3>();
            var trisA = new List<int>();
            var trisB = new List<int>();

            for (int i = 0; i < Samples; i++)
            {
                int j = (i + 1) % Samples;
                var tris = ((i / 3) % 2 == 0) ? trisA : trisB;
                Vector3 outDir0 = right[i] * outSign, outDir1 = right[j] * outSign;
                Vector3 b0 = center[i] + right[i] * offset, b1 = center[j] + right[j] * offset;
                Vector3 up = Vector3.up * WallHeight;

                // road-facing side, top, back side
                AddQuad(verts, tris, b0, b0 + up, b1 + up, b1, -outDir0);
                AddQuad(verts, tris, b0 + up, b0 + up + outDir0 * WallThickness, b1 + up + outDir1 * WallThickness, b1 + up, Vector3.up);
                AddQuad(verts, tris, b0 + outDir0 * WallThickness, b0 + up + outDir0 * WallThickness, b1 + up + outDir1 * WallThickness, b1 + outDir1 * WallThickness, outDir0);
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisA, 0);
            mesh.SetTriangles(trisB, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, name);

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a, b };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // Adds a quad (own vertices => flat shading) wound so that its normal faces `hint`.
        static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 hint)
        {
            int s = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(n, hint) >= 0f)
            { tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); tris.Add(s); tris.Add(s + 2); tris.Add(s + 3); }
            else
            { tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); tris.Add(s); tris.Add(s + 3); tris.Add(s + 2); }
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"Assets/Meshes/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void BuildStartLine(Transform parent, Material checker, Material dark)
        {
            Vector3 p = center[0] + Vector3.up * 0.02f;
            var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = "StartLine";
            line.transform.SetParent(parent);
            line.transform.position = p;
            line.transform.rotation = Quaternion.LookRotation(tangent[0], Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            line.transform.localScale = new Vector3(RoadWidth, 3f, 1f);
            line.GetComponent<MeshRenderer>().sharedMaterial = checker;
            Object.DestroyImmediate(line.GetComponent<Collider>());

            // Gantry
            float half = RoadWidth * 0.5f + 1f;
            for (int s = -1; s <= 1; s += 2)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "GantryPillar";
                pillar.transform.SetParent(parent);
                pillar.transform.position = center[0] + right[0] * (half * s) + Vector3.up * 4f;
                pillar.transform.localScale = new Vector3(0.9f, 4f, 0.9f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = dark;
                Object.DestroyImmediate(pillar.GetComponent<Collider>());
            }
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "GantryBanner";
            beam.transform.SetParent(parent);
            beam.transform.position = center[0] + Vector3.up * 8.2f;
            beam.transform.rotation = Quaternion.LookRotation(tangent[0], Vector3.up);
            beam.transform.localScale = new Vector3(half * 2f + 1f, 1.6f, 0.8f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = checker;
            Object.DestroyImmediate(beam.GetComponent<Collider>());
        }

        // ---------------------------------------------------------------- kart

        struct Rival
        {
            public string name; public Color body, suit; public float skill, lane;
            public Rival(string n, Color b, Color s, float sk, float l) { name = n; body = b; suit = s; skill = sk; lane = l; }
        }

        static void GridSlot(int slot, out Vector3 pos, out float yaw)
        {
            int row = slot / 2, col = slot % 2;
            int idx = Samples - 4 - row * 3;
            pos = center[idx] + right[idx] * (col == 0 ? -3.2f : 3.2f) + Vector3.up * 0.6f;
            yaw = Mathf.Atan2(tangent[idx].x, tangent[idx].z) * Mathf.Rad2Deg;
        }

        static void AddRacer(KartController kart, RaceManager race, string displayName, bool isPlayer, Color color, Vector3 pos, float yaw)
        {
            var r = kart.gameObject.AddComponent<Racer>();
            r.displayName = displayName;
            r.isPlayer = isPlayer;
            r.color = color;
            r.race = race;
            r.startPosition = pos;
            r.startYaw = yaw;
            race.racers.Add(r);
            kart.gameObject.AddComponent<KartItems>();
            kart.gameObject.AddComponent<KartAudio>();
        }

        // Rows of item boxes across the road at a few points around the lap.
        static void BuildItemBoxes(Transform parent, Material outer, Material inner)
        {
            int[] rows = { 28, 88, 148, 208 };
            float[] lanes = { -5.2f, -1.75f, 1.75f, 5.2f };
            foreach (int idx in rows)
                foreach (float lane in lanes)
                {
                    var go = new GameObject("ItemBox");
                    go.transform.SetParent(parent);
                    go.transform.position = center[idx] + right[idx] * lane + Vector3.up * 1.3f;
                    var col = go.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = Vector3.one * 2.2f;

                    var vis = new GameObject("Visual").transform;
                    vis.SetParent(go.transform, false);
                    Prim(vis, "Outer", PrimitiveType.Cube, Vector3.zero, Vector3.one * 1.3f, outer);
                    var core = Prim(vis, "Core", PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.7f, inner);
                    core.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

                    var box = go.AddComponent<ItemBox>();
                    box.visual = vis;
                }
        }

        static PhysicsMaterial GetSpherePhysicsMaterial()
        {
            string pmPath = "Assets/Physics/KartSphere.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("KartSphere");
                AssetDatabase.CreateAsset(pm, pmPath);
            }
            pm.dynamicFriction = 0f;
            pm.staticFriction = 0f;
            pm.bounciness = 0.15f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static KartController BuildKart(string name, Vector3 pos, float yaw, Material red, Material dark, Material white, Material skin, Material suit)
        {
            var kartGo = new GameObject(name);
            kartGo.transform.position = pos - Vector3.up * 0.5f;
            kartGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var sphereGo = new GameObject(name + "_Sphere");
            sphereGo.transform.position = pos;
            var sc = sphereGo.AddComponent<SphereCollider>();
            sc.radius = 0.5f;
            sc.sharedMaterial = GetSpherePhysicsMaterial();
            var rb = sphereGo.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(kartGo.transform, false);

            Prim(visual.transform, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.3f, 0.3f, 2.1f), red);
            Prim(visual.transform, "Nose", PrimitiveType.Cube, new Vector3(0f, 0.38f, 1.25f), new Vector3(0.9f, 0.22f, 0.8f), red);
            Prim(visual.transform, "Stripe", PrimitiveType.Cube, new Vector3(0f, 0.585f, 0.1f), new Vector3(0.35f, 0.02f, 2.15f), white);
            Prim(visual.transform, "FrontBumper", PrimitiveType.Cube, new Vector3(0f, 0.3f, 1.7f), new Vector3(1.5f, 0.14f, 0.18f), dark);
            Prim(visual.transform, "SpoilerWing", PrimitiveType.Cube, new Vector3(0f, 1.05f, -1.05f), new Vector3(1.6f, 0.08f, 0.38f), red);
            Prim(visual.transform, "SpoilerL", PrimitiveType.Cube, new Vector3(-0.5f, 0.78f, -1.0f), new Vector3(0.08f, 0.5f, 0.2f), dark);
            Prim(visual.transform, "SpoilerR", PrimitiveType.Cube, new Vector3(0.5f, 0.78f, -1.0f), new Vector3(0.08f, 0.5f, 0.2f), dark);
            Prim(visual.transform, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.35f), new Vector3(0.8f, 0.5f, 0.2f), dark);
            Prim(visual.transform, "Driver", PrimitiveType.Sphere, new Vector3(0f, 0.95f, -0.1f), new Vector3(0.62f, 0.62f, 0.62f), suit);
            Prim(visual.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.42f, -0.1f), new Vector3(0.5f, 0.5f, 0.5f), skin);
            Prim(visual.transform, "Cap", PrimitiveType.Sphere, new Vector3(0f, 1.57f, -0.06f), new Vector3(0.52f, 0.34f, 0.52f), red);
            Prim(visual.transform, "Exhaust", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, -1.15f), new Vector3(0.2f, 0.12f, 0.2f), dark).transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var wheels = new List<Transform>();
            var front = new List<Transform>();
            float[] xs = { -0.85f, 0.85f };
            float[] zs = { 0.95f, -0.95f };
            foreach (float z in zs)
                foreach (float x in xs)
                {
                    bool isFront = z > 0f;
                    float radius = isFront ? 0.3f : 0.36f;
                    var pivot = new GameObject(isFront ? "WheelFront" : "WheelRear").transform;
                    pivot.SetParent(visual.transform, false);
                    pivot.localPosition = new Vector3(x, radius, z);
                    var tire = Prim(pivot, "Tire", PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, isFront ? 0.14f : 0.2f, radius * 2f), dark);
                    tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var hub = Prim(pivot, "Hub", PrimitiveType.Cylinder, new Vector3(Mathf.Sign(x) * 0.1f, 0f, 0f), new Vector3(radius * 1.1f, 0.03f, radius * 1.1f), white);
                    hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheels.Add(pivot);
                    if (isFront) front.Add(pivot);
                }

            var ctrl = kartGo.AddComponent<KartController>();
            ctrl.sphere = rb;
            ctrl.visual = visual.transform;
            ctrl.frontWheels = front.ToArray();
            ctrl.allWheels = wheels.ToArray();
            return ctrl;
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ---------------------------------------------------------------- lighting & post

        static void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 520f;
        }

        static void BuildPostProcessing()
        {
            string path = "Assets/Settings/KartVolumeProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.65f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.4f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(8f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        // ---------------------------------------------------------------- HUD

        static void BuildHud(RaceManager race, KartController kart)
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var hud = canvasGo.AddComponent<RaceHud>();
            hud.race = race;
            hud.kart = kart;

            var t = canvasGo.transform;
            hud.lapText = MakeText(t, "Lap", new Vector2(0f, 1f), new Vector2(40f, -30f), 64, TextAnchor.UpperLeft, 600f);
            hud.timeText = MakeText(t, "Time", new Vector2(0f, 1f), new Vector2(40f, -110f), 48, TextAnchor.UpperLeft, 600f);
            hud.bestText = MakeText(t, "Best", new Vector2(0f, 1f), new Vector2(40f, -170f), 36, TextAnchor.UpperLeft, 600f);
            hud.speedText = MakeText(t, "Speed", new Vector2(1f, 0f), new Vector2(-40f, 40f), 80, TextAnchor.LowerRight, 600f);
            hud.helpText = MakeText(t, "Help", new Vector2(0f, 0f), new Vector2(40f, 30f), 26, TextAnchor.LowerLeft, 1100f);
            hud.helpText.text = "W/S gas / brake    A/D steer    SPACE drift (release for mini-turbo)    E use item    R respawn    ESC pause";
            hud.itemText = MakeText(t, "Item", new Vector2(0.5f, 1f), new Vector2(0f, -30f), 56, TextAnchor.UpperCenter, 700f);
            hud.itemText.supportRichText = true;
            hud.helpText.color = new Color(1f, 1f, 1f, 0.8f);

            hud.positionText = MakeText(t, "Position", new Vector2(1f, 1f), new Vector2(-40f, -20f), 120, TextAnchor.UpperRight, 500f);
            hud.positionText.supportRichText = true;
            hud.standingsText = MakeText(t, "Standings", new Vector2(1f, 1f), new Vector2(-40f, -170f), 30, TextAnchor.UpperRight, 400f);
            hud.standingsText.supportRichText = true;
            hud.standingsText.rectTransform.sizeDelta = new Vector2(400f, 300f);

            hud.centerText = MakeText(t, "Center", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), 140, TextAnchor.MiddleCenter, 1400f);
            hud.centerText.rectTransform.sizeDelta = new Vector2(1400f, 700f);
            hud.centerText.supportRichText = true;

            var barGo = new GameObject("DriftBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(t, false);
            var rt = (RectTransform)barGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            rt.sizeDelta = new Vector2(420f, 16f);
            hud.driftBar = barGo.GetComponent<Image>();
            hud.driftBar.enabled = false;

            // Title / pause menu (last sibling => drawn on top)
            var panel = new GameObject("MenuPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(t, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var dim = panel.GetComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.08f, 0.62f);
            dim.raycastTarget = false;

            var menu = canvasGo.AddComponent<MenuManager>();
            menu.race = race;
            menu.panel = panel;
            menu.titleText = MakeText(panel.transform, "MenuTitle", new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), 150, TextAnchor.MiddleCenter, 1400f);
            menu.titleText.color = new Color(1f, 0.85f, 0.2f);
            menu.titleText.rectTransform.sizeDelta = new Vector2(1400f, 220f);
            menu.menuText = MakeText(panel.transform, "MenuItems", new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), 64, TextAnchor.UpperCenter, 1000f);
            menu.menuText.supportRichText = true;
            menu.menuText.rectTransform.sizeDelta = new Vector2(1000f, 420f);
            menu.hintText = MakeText(panel.transform, "MenuHint", new Vector2(0.5f, 0f), new Vector2(0f, 60f), 30, TextAnchor.LowerCenter, 1500f);
            menu.hintText.color = new Color(1f, 1f, 1f, 0.75f);
            hud.menu = menu;
        }

        static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, int size, TextAnchor align, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, size * 1.5f);
            var txt = go.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = align;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            var ol = go.GetComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(2f, -2f);
            return txt;
        }

        // ---------------------------------------------------------------- assets

        static Material GetMat(string name, Color color, float smoothness)
        {
            string path = $"Assets/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GetCheckerMaterial()
        {
            string texPath = "Assets/Textures/Checker.png";
            if (!File.Exists(texPath))
            {
                const int w = 16, h = 4;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, y, ((x + y) % 2 == 0) ? Color.white : new Color(0.05f, 0.05f, 0.05f));
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            var mat = GetMat("Checker", Color.white, 0.2f);
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            return mat;
        }
    }
}
