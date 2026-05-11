using System;
using UnityEngine;
using UnityEngine.UIElements;

public interface IUIScreen : IDisposable
{
    public void Show();
    public void Hide();
    public void DoUpdate();
}

/// <summary>
/// A UIScreen is a single menu (e.g. a main menu or a pause screen).
/// Using pattern explained here: https://discussions.unity.com/t/quizu-managing-menu-screens-in-ui-toolkit/310272
/// </summary>
public abstract class UIScreen : IUIScreen
{
    protected UIScreenManager uiScreenManager { get; private set; }

    protected VisualElement Root { get; private set; }

    public bool IsHidden { get; private set; }

    public UIScreen(UIScreenManager manager, VisualElement root)
    {
        uiScreenManager = manager;
        Root = root;
    }

    public void Show()
    {
        Root.style.display = DisplayStyle.Flex;
        IsHidden = false;
        OnShow();
    }
    protected abstract void OnShow();

    public void Hide()
    {
        Root.style.display = DisplayStyle.None;
        IsHidden = true;
        OnHide();
    }
    protected abstract void OnHide();

    public virtual void DoUpdate()
    {
    }

    public virtual void Dispose()
    {
    }
}