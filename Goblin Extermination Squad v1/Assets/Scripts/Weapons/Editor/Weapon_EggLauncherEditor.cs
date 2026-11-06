using UnityEditor;
using UnityEngine;

// Creates opt-in starter assets, never edits a scene or equips the weapon automatically. All visual parts remain
// real editable MeshFilter/MeshRenderer GameObjects. Select the resulting prefab to author your own launcher.
// GC: editor-only mesh/material/prefab/SerializedObject allocations on the menu action. No runtime subscriptions.
public static class Weapon_EggLauncherEditor
{
    private const string Folder = "Assets/Prefabs/Weapons/Egg Launcher";
    private const string WeaponPath = Folder + "/Egg Launcher.prefab";

    [MenuItem("Goblins/Weapons/Create Egg Launcher Starter Prefabs")]
    private static void CreateStarter()
    {
        Weapon_EggLauncher existing = AssetDatabase.LoadAssetAtPath<Weapon_EggLauncher>(WeaponPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        // A unique folder protects any partial/manual setup if a launcher doesn't exist at the expected path.
        string folder = AssetDatabase.IsValidFolder(Folder) ? AssetDatabase.GenerateUniqueAssetPath(Folder) : Folder;
        EnsureFolder(folder);
        Material shell = MaterialAsset(folder, "Egg Shell", new Color(1f, 0.94f, 0.75f));
        Material yolk = MaterialAsset(folder, "Yolk", new Color(1f, 0.65f, 0.06f));
        Material metal = MaterialAsset(folder, "Launcher", new Color(0.36f, 0.25f, 0.4f));

        GameObject crack = new GameObject("Egg Crack");
        Effect_Hit impact;
        try
        {
            Effect_Hit hit = crack.AddComponent<Effect_Hit>();
            hit.lifetime = 0.6f;
            hit.facing = Effect_Hit.Facing.YAlongNormal;
            Part(crack.transform, "Yolk", PrimitiveType.Sphere, yolk, new Vector3(0f, 0.035f, 0f), new Vector3(0.8f, 0.06f, 0.65f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 25f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.42f, 0.07f, 0f);
                GameObject piece = Part(crack.transform, "Shell " + (i + 1), PrimitiveType.Cube, shell, offset, new Vector3(0.23f, 0.035f, 0.19f));
                piece.transform.localRotation = Quaternion.Euler(15f, angle, 30f);
            }
            impact = PrefabUtility.SaveAsPrefabAsset(crack, folder + "/Egg Crack.prefab").GetComponent<Effect_Hit>();
        }
        finally { Object.DestroyImmediate(crack); }

        GameObject egg = new GameObject("Egg Projectile");
        Combat_ProjectileType eggType;
        try
        {
            Combat_ProjectileType type = egg.AddComponent<Combat_ProjectileType>();
            type.gravity = 25f;
            type.hitRadius = 0.2f;
            type.linger = 0f;
            type.explodes = false;
            type.surfaceImpact = impact;
            type.damageableImpact = impact;
            // Ground + Building only, excluding the plane and other projectile/effect visuals.
            type.hitLayers = LayerMask.GetMask("Ground", "Building");
            if (type.hitLayers.value == 0)
                Debug.LogWarning("Egg projectile: assign your ground/scenery layers to Hit Layers before testing.");
            GameObject model = Part(egg.transform, "Egg Shell", PrimitiveType.Sphere, shell, Vector3.zero, Vector3.one);
            Mesh mesh = Object.Instantiate(model.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "Egg";
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float taper = 1f - v.z * 0.35f;
                vertices[i] = new Vector3(v.x * 0.65f * taper, v.y * 0.65f * taper, v.z * 0.95f);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/Egg.asset");
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            eggType = PrefabUtility.SaveAsPrefabAsset(egg, folder + "/Egg Projectile.prefab").GetComponent<Combat_ProjectileType>();
        }
        finally { Object.DestroyImmediate(egg); }

        GameObject weapon = new GameObject("Egg Launcher");
        try
        {
            Weapon_EggLauncher launcher = weapon.AddComponent<Weapon_EggLauncher>();
            Part(weapon.transform, "Launcher Body", PrimitiveType.Cube, metal, new Vector3(0f, -0.4f, 0.4f), new Vector3(0.85f, 0.7f, 1.5f));
            GameObject barrel = Part(weapon.transform, "Barrel", PrimitiveType.Cylinder, shell, new Vector3(0f, -0.1f, 1.2f), new Vector3(0.65f, 0.65f, 0.65f));
            barrel.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            GameObject tip = new GameObject("Muzzle");
            tip.transform.SetParent(weapon.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.15f, 1.85f);
            Weapon_Muzzle muzzle = tip.AddComponent<Weapon_Muzzle>();
            SerializedObject data = new SerializedObject(launcher);
            data.FindProperty("projectile").objectReferenceValue = eggType;
            data.FindProperty("previewMuzzle").objectReferenceValue = muzzle;
            SerializedProperty groups = data.FindProperty("muzzleGroups");
            groups.arraySize = 1;
            SerializedProperty muzzles = groups.GetArrayElementAtIndex(0).FindPropertyRelative("muzzles");
            muzzles.arraySize = 1;
            muzzles.GetArrayElementAtIndex(0).objectReferenceValue = muzzle;
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(weapon, folder + "/Egg Launcher.prefab");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        finally { Object.DestroyImmediate(weapon); }
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    private static Material MaterialAsset(string folder, string name, Color colour)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
        return material;
    }

    private static GameObject Part(Transform parent, string name, PrimitiveType shape, Material material, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }
}
