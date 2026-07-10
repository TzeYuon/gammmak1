using Unity.Entities;
using Unity.Mathematics;
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
    /// <summary>
    /// Higher number prefers higher density
    /// </summary>
    public float DensityBias = 1.0f;
    /// <summary>
    /// Higher number prefers less rotation
    /// </summary>
    public float AngleBias = 2.0f; 
    /// <summary>
    /// Higher number prefers closer targets, larger negative prefers far targets
    /// </summary>
    public float DistanceBias = 2.0f;

    // Rotation
    public bool HasTurret = true;
    public float TurnSpeed = 5.0f;
    public Vector3 BaseDirection = new(0, 1, 0);
    public float MaxAngle = 180f;

    // Firing
    public FiringType Firing = FiringType.Hitscan;
    public GameObject ProjectilePrefab;
    public Transform WeaponStartPoint;
    public float HitRadius;
    public float ExplosionRadius;
    public int PierceCount;
    public float ProjectileSpeed;
    public float ProjectileTurnSpeed;
    public float Spread;
    public float TriggerTime;

    class Baker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new WeaponState());

            // Targeting
            AddComponent(entity, new WeaponTargetingStats { 
                Range = authoring.Range,
                RangeSq = authoring.Range * authoring.Range,
                DensityBias = authoring.DensityBias,
                AngleBias = authoring.AngleBias,
                DistanceBias = authoring.DistanceBias,
            });

            // Rotation
            if (authoring.HasTurret)
                AddComponent(entity, new TurretComponent { TurnSpeed = authoring.TurnSpeed });

            AddComponent(entity, new WeaponFiringArc {
                RestingRotation = quaternion.RotateZ(math.atan2(authoring.BaseDirection.y, authoring.BaseDirection.x) - (math.PI / 2f)),
                MaxAngle = authoring.MaxAngle
            });

            // Firing
            AddComponent(entity, new WeaponCombatStats
            {
                Damage = authoring.Damage,
                FireRate = authoring.FireRate,
                Spread = authoring.Spread,

                ProjectileSpeed = authoring.ProjectileSpeed,
                ProjectileTurnSpeed = authoring.ProjectileTurnSpeed,
                HitRadius = authoring.HitRadius,
                ExplosionRadiusSq = authoring.ExplosionRadius * authoring.ExplosionRadius,
                PierceCount = authoring.PierceCount,
                TriggerTime = authoring.TriggerTime,
            });
            if (authoring.Firing == FiringType.Hitscan)
            {
                AddComponent(entity, new HitscanWeapon()
                {
                    WeaponStartOffset = new float2(authoring.WeaponStartPoint.position.x, authoring.WeaponStartPoint.position.y),
                });
            }
            else
            {
                AddComponent(entity, new ProjectileWeapon {
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    WeaponStartOffset = new float2(authoring.WeaponStartPoint.position.x, authoring.WeaponStartPoint.position.y),
                });
            }

            AddComponent(entity, new ShipReference());
            AddComponent<NeedsShipReferenceTag>(entity);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WeaponAuthoring))]
public class WeaponAuthoringEditor : Editor
{
    // Properties
    SerializedProperty p_Damage, p_FireRate, p_Range;
    SerializedProperty p_DensityBias, p_AngleBias, p_DistanceBias;
    SerializedProperty p_HasTurret, p_TurnSpeed, p_RestingDirection, p_MaxAngleLimit;
    SerializedProperty p_Firing, p_ProjectilePrefab, p_WeaponStartPoint, p_HitRadius, p_ExplosionRadius, p_PierceCount, p_ProjectileSpeed, p_ProjectileTurnSpeed, p_Spread, p_TriggerTime;

    private void OnEnable()
    {
        p_Damage = serializedObject.FindProperty("Damage");
        p_FireRate = serializedObject.FindProperty("FireRate");
        p_Range = serializedObject.FindProperty("Range");

        p_DensityBias = serializedObject.FindProperty("DensityBias");
        p_AngleBias = serializedObject.FindProperty("AngleBias");
        p_DistanceBias = serializedObject.FindProperty("DistanceBias");

        p_HasTurret = serializedObject.FindProperty("HasTurret");
        p_TurnSpeed = serializedObject.FindProperty("TurnSpeed");
        p_RestingDirection = serializedObject.FindProperty("BaseDirection");
        p_MaxAngleLimit = serializedObject.FindProperty("MaxAngle");

        p_Firing = serializedObject.FindProperty("Firing");
        p_ProjectilePrefab = serializedObject.FindProperty("ProjectilePrefab");
        p_WeaponStartPoint = serializedObject.FindProperty("WeaponStartPoint");
        p_HitRadius = serializedObject.FindProperty("HitRadius");
        p_ExplosionRadius = serializedObject.FindProperty("ExplosionRadius");
        p_PierceCount = serializedObject.FindProperty("PierceCount");
        p_ProjectileSpeed = serializedObject.FindProperty("ProjectileSpeed");
        p_ProjectileTurnSpeed = serializedObject.FindProperty("ProjectileTurnSpeed");
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
        EditorGUILayout.PropertyField(p_DensityBias, new GUIContent("Density Bias"));
        EditorGUILayout.PropertyField(p_AngleBias, new GUIContent("Angle Bias (Laziness)"));
        EditorGUILayout.PropertyField(p_DistanceBias, new GUIContent("Distance Bias"));
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
            EditorGUILayout.PropertyField(p_ProjectileTurnSpeed);
            EditorGUILayout.PropertyField(p_Spread);
            EditorGUILayout.PropertyField(p_TriggerTime);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(p_WeaponStartPoint);
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif