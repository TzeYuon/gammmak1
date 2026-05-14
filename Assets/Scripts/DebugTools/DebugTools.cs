#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public class DebugTools : MonoBehaviour
{
    [System.Serializable]
    public class ToolState
    {
        public string ToolName;
        public GameObject TargetObject;
        public bool IsActive;
    }

    [Header("Debug Tools")]
    public List<ToolState> Tools = new();

    private void OnValidate()
    {
        foreach (var tool in Tools)
        {
            if (tool.TargetObject != null)
            {
                tool.ToolName = tool.TargetObject.name;

                if (tool.TargetObject.activeSelf != tool.IsActive)
                {
#if UNITY_EDITOR
                    bool desiredState = tool.IsActive;
                    GameObject target = tool.TargetObject;

                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (target != null)
                            target.SetActive(desiredState);
                    };
#endif
                }
            }
        }
    }

    [ContextMenu("Auto-Populate From Children")]
    private void PopulateFromChildren()
    {
        Tools.Clear();
        foreach (Transform child in transform)
        {
            Tools.Add(new ToolState
            {
                ToolName = child.name,
                TargetObject = child.gameObject,
                IsActive = child.gameObject.activeSelf
            });
        }
    }
}
#endif