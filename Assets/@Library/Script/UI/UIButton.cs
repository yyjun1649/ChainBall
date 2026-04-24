using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButton : MonoBehaviour
{
    [SerializeField] private GameObject _goOff;   // 비활성 상태 시 보여줄 오브젝트
    [SerializeField] private GameObject _goOn;    // 활성 상태 시 보여줄 오브젝트

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        UpdateVisual(_button.interactable);
    }
    
    public void UpdateVisual(bool isOn)
    {
        _goOn.SetActive(isOn);
        _goOff.SetActive(!isOn);
    }
    
    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
        UpdateVisual(interactable);
    }
}