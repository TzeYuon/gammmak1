using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("PipContainer")] 
public partial class PipContainerVisualElement : VisualElement
{
    private int m_Value;
    private int m_MaxPips;

    [UxmlAttribute]
    public int Value
    {
        get => m_Value;
        set { 
            m_Value = Mathf.Clamp(value, 0, m_MaxPips); 
            Rebuild(); 
        }
    }

    [UxmlAttribute]
    public int MaxPips
    {
        get => m_MaxPips;
        set { 
            m_MaxPips = Mathf.Max(0, value); 
            Rebuild(); 
        }
    }

    public PipContainerVisualElement()
    {
        AddToClassList("pip-container");
        Rebuild();
    }

    private void Rebuild()
    {
        Clear(); // Remove old pips

        for (int i = 0; i < m_MaxPips; i++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("pip");

            if (i < m_Value)
            {
                pip.AddToClassList("pip--active");
            }
            else
            {
                pip.AddToClassList("pip--inactive");
            }

            Add(pip);
        }
    }
}