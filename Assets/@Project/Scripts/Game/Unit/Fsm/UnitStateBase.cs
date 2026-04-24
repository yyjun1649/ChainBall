
    using System.Collections;
    using Library;
    using UnityEngine;

    public abstract class UnitStateBase : MonoBehaviour,IState
    {
        public eStateType stateType;
        
        protected UnitController _owner;
        
        public void Initialize(UnitController controller)
        {
            _owner = controller;
        }

        public abstract void Enter();

        public abstract IEnumerator Execute();

        public abstract void Exit();
    }
