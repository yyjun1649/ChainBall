using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Library
{
    public class StateMachine<T>
    {
        private readonly Dictionary<T, IState> _states = new();
        private readonly MonoBehaviour _mono;
        private Coroutine _routine;
        private bool _isTransitioning;

        public IState Current { get; private set; }
        public T CurrentState { get; private set; }

        public StateMachine(MonoBehaviour mono)
        {
            if (mono == null)
            {
                Debug.LogError("[StateMachine] host MonoBehaviour is null — coroutines cannot run.");
            }
            _mono = mono;
        }

        public void Add(T key, IState state)
        {
            _states.TryAdd(key, state);
        }

        public void Clear()
        {
            _states.Clear();
        }

        public void ChangeState(T key)
        {
            // Re-entrancy guard: ChangeState called from inside Enter/Exit/Execute would corrupt teardown order.
            if (_isTransitioning)
            {
                Debug.LogWarning($"[StateMachine] ChangeState({key}) ignored — a transition is already in progress.");
                return;
            }

            if (!_states.TryGetValue(key, out var state))
            {
                return;
            }

            _isTransitioning = true;
            try
            {
                if (_routine != null)
                {
                    _mono.StopCoroutine(_routine);
                    _routine = null;
                }

                if (Current != null)
                {
                    Current.Exit();
                }

                CurrentState = key;
                Current = state;
                Current.Enter();
                _routine = _mono.StartCoroutine(Current.Execute());
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public T GetCurrentState()
        {
            return CurrentState;
        }
    }


    public interface IState
    {
        void Enter();
        IEnumerator Execute();
        void Exit();
    }

    public enum eStateType
    {
        Idle,
        Move,
        Death,
        Attack,
    }
}
