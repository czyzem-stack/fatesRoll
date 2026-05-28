using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    public GameObject targetPanel;

    public void OpenPanel()
    {
        if (targetPanel != null)
            targetPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (targetPanel != null)
            targetPanel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (targetPanel != null)
            targetPanel.SetActive(!targetPanel.activeSelf);
    }
}
