using Unity.Entities;
using UnityEditor;
using UnityEngine;

public enum TargetingType { Heatmap, Precision }
public enum FiringType { Hitscan, Projectile }

class WeaponAuthoring : MonoBehaviour
{
    // Base Stats
    public float Damage = 10f;
    public float FireRate = 0.2f;
    public float Range = 30f;

    // Targeting
    public TargetingType Targeting = TargetingType.Heatmap;
    public float AngleBias = 2.0f;

    // Rotation
    public bool HasTurret = true;
    public float TurnSpeed = 5.0f;
    public Vector3 BaseDirection = new(0, 1, 0);
    public float MaxAngle = 180f;

    // Firing
    public FiringType Firing = FiringType.Hitscan;
    public GameObject ProjectilePrefab;
    public float HitRadius;
    public float ExplosionRadius;
    public int PierceCount;
    public float ProjectileSpeed;
    public float Spread;
    public float TriggerTime;

    class Baker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new WeaponState { FireRate = authoring.FireRate });

            // Targeting
            if (authoring.Targeting == TargetingType.Heatmap)
                AddComponent(entity, new HeatmapTargetingComponent { Range = authoring.Range, AngleBias = authoring.AngleBias });
            else
                AddComponent(entity, new PrecisionTargetingComponent { Range = authoring.Range, AngleBias = authoring.AngleBias });

            // Rotation
            if (authoring.HasTurret)
                AddComponent(entity, new TurretComponent { TurnSpeed = authoring.TurnSpeed });

            AddComponent(entity, new WeaponFiringArc {
                BaseDirection = authoring.BaseDirection,
                MaxAngle = authoring.MaxAngle
            });

            // Firing
            if (authoring.Firing == FiringType.Hitscan)
            {
                AddComponent(entity, new HitscanWeapon { Damage = authoring.Damage });
            }
            else
            {
                AddComponent(entity, new ProjectileWeapon { 
                    Damage = authoring.Damage,
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    HitRadius = authoring.HitRadius,
                    ExplosionRadiusSq = authoring.ExplosionRadius * authoring.ExplosionRadius,
                    PierceCount = authoring.PierceCount,
                    ProjectileSpeed = authoring.ProjectileSpeed,
                    Spread = authoring.Spread,
                    TriggerTime = authoring.TriggerTime,
                });
            }

            AddComponent(entity, new ShipReference());
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WeaponAuthoring))]
public class WeaponAuthoringEditor : Editor
{
    // Properties
    SerializedProperty p_Damage, p_FireRate, p_Range;
    SerializedProperty p_Targeting, p_AngleBias;
    SerializedProperty p_HasTurret, p_TurnSpeed, p_RestingDirection, p_MaxAngleLimit;
    SerializedProperty p_Firing, p_ProjectilePrefab, p_HitRadius, p_ExplosionRadius, p_PierceCount, p_ProjectileSpeed, p_Spread, p_TriggerTime;

    private void OnEnable()
    {
        p_Damage = serializedObject.FindProperty("Damage");
        p_FireRate = serializedObject.FindProperty("FireRate");
        p_Range = serializedObject.FindProperty("Range");

        p_Targeting = serializedObject.FindProperty("Targeting");
        p_AngleBias = serializedObject.FindProperty("AngleBias");

        p_HasTurret = serializedObject.FindProperty("HasTurret");
        p_TurnSpeed = serializedObject.FindProperty("TurnSpeed");
        p_RestingDirection = serializedObject.FindProperty("BaseDirection");
        p_MaxAngleLimit = serializedObject.FindProperty("MaxAngle");

        p_Firing = serializedObject.FindProperty("Firing");
        p_ProjectilePrefab = serializedObject.FindProperty("ProjectilePrefab");
        p_HitRadius = serializedObject.FindProperty("HitRadius");
        p_ExplosionRadius = serializedObject.FindProperty("ExplosionRadius");
        p_PierceCount = serializedObject.FindProperty("PierceCount");
        p_ProjectileSpeed = serializedObject.FindProperty("ProjectileSpeed");
        p_Spread = serializedObject.FindProperty("Spread");
        p_TriggerTime = serializedObject.FindProperty("TriggerTime");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Base stats
        EditorGUILayout.LabelField("CORE STATS");
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.PropertyField(p_Damage);
        EditorGUILayout.PropertyField(p_FireRate);
        EditorGUILayout.PropertyField(p_Range);
        EditorGUILayout.EndVertical();

        // Targeting
        EditorGUILayout.LabelField("TARGETING");
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.PropertyField(p_Targeting);
        EditorGUILayout.PropertyField(p_AngleBias, new GUIContent("Angle Bias (Laziness)"));
        EditorGUILayout.EndVertical();

        // Rotation
        EditorGUILayout.LabelField("ROTATION");
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.PropertyField(p_RestingDirection);
        EditorGUILayout.PropertyField(p_MaxAngleLimit, new GUIContent("Max Firing Arc (Degrees)"));

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(p_HasTurret);

        if (p_HasTurret.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(p_TurnSpeed);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // Firing
        EditorGUILayout.LabelField("FIRING");
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.PropertyField(p_Firing);

        if (p_Firing.enumValueIndex == (int)FiringType.Projectile)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(p_ProjectilePrefab);
            EditorGUILayout.PropertyField(p_HitRadius);
            EditorGUILayout.PropertyField(p_ExplosionRadius);
            EditorGUILayout.PropertyField(p_PierceCount);
            EditorGUILayout.PropertyField(p_ProjectileSpeed);
            EditorGUILayout.PropertyField(p_Spread);
            EditorGUILayout.PropertyField(p_TriggerTime);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif