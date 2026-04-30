    using System.Collections;
    using Library;
    using UnityEngine;

    public abstract class PhaseBase : MonoBehaviour, IState
    {
        public void Enter()
        {

        }

        public abstract IEnumerator Execute();

        public void Exit()
        {

        }
    }