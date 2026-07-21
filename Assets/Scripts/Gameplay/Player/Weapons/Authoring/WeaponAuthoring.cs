using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public enum TargetingType { Heatmap, Precision }
public enum FiringType { Hitscan, Projectile }

class WeaponAuthoring : MonoBehaviour
{
    // Firing
    public FiringType Firing = FiringType.Hitscan;
    public GameObject ProjectilePrefab;
    public Transform WeaponStartPoint;

    // vfx
    public Color VFXColour;

    // sfx
    public AudioType SoundEffect;
    class Baker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new WeaponState());

            // Targeting
            AddComponent(entity, new WeaponTargetingStats());
            AddComponent(entity, new WeaponFiringArc());

            // Firing
            AddComponent(entity, new WeaponCombatStats());

            if (authoring.Firing == FiringType.Hitscan)
            {
                AddComponent(entity, new HitscanWeapon()
                {
                    WeaponStartOffset = new float2(authoring.WeaponStartPoint.position.x, authoring.WeaponStartPoint.position.y),
                    VFXColour = new float4(authoring.VFXColour.r, authoring.VFXColour.g, authoring.VFXColour.b, 3),
                    AudioType = authoring.SoundEffect
                });
            }
            else
            {
                AddComponent(entity, new ProjectileWeapon {
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    WeaponStartOffset = new float2(authoring.WeaponStartPoint.position.x, authoring.WeaponStartPoint.position.y),
                    VFXColour = new float4(authoring.VFXColour.r, authoring.VFXColour.g, authoring.VFXColour.b, 3),
                    AudioType = authoring.SoundEffect
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
    SerializedProperty p_Firing, p_ProjectilePrefab, p_WeaponStartPoint;
    SerializedProperty p_VFXColour;
    SerializedProperty p_SoundEffect;

    private void OnEnable()
    {
        p_Firing = serializedObject.FindProperty("Firing");
        p_ProjectilePrefab = serializedObject.FindProperty("ProjectilePrefab");
        p_WeaponStartPoint = serializedObject.FindProperty("WeaponStartPoint");

        p_VFXColour = serializedObject.FindProperty("VFXColour");
        p_SoundEffect = serializedObject.FindProperty("SoundEffect");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Firing
        EditorGUILayout.LabelField("FIRING");
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.PropertyField(p_Firing);

        if (p_Firing.enumValueIndex == (int)FiringType.Projectile)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(p_ProjectilePrefab);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(p_WeaponStartPoint);
        EditorGUILayout.EndVertical();

        // VFX
        EditorGUILayout.LabelField("VFX");
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.PropertyField(p_VFXColour);
        EditorGUILayout.PropertyField(p_SoundEffect);
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif