using Library;
using UnityEngine;

public class UnitFsmHandler : MonoBehaviour
{
    private StateMachine<eStateType> _fsm;
    
    protected UnitController _unitController;

    public void Change(eStateType type)
    {
        _fsm.ChangeState(type);
    } 

    public void Initialize(UnitController unitController)
    {
        _fsm ??= new StateMachine<eStateType>(this);

        _unitController = unitController;
        
        RegisterState(GetComponentsInChildren<UnitStateBase>());
    }

    public void RegisterState(UnitStateBase[] states)
    {
        _fsm.Clear();
        
        foreach (var state in states)
        {
            _fsm.Add(state.stateType, state);
            state.Initialize(_unitController);
        }
    }
}