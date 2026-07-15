using UnityEngine;

public class MenuToggle : MonoBehaviour
{
    [Tooltip("Drag the entire Level Editor Menu GameObject here.")]
    public GameObject menuPanel;

    /// <summary>
    /// Call this from your Toggle Button's OnClick() event to show or hide the menu.
    /// </summary>
    public void ToggleMenu()
    {
        if (menuPanel != null)
        {
            bool isActive = menuPanel.activeSelf;
            menuPanel.SetActive(!isActive);
        }
    }
}