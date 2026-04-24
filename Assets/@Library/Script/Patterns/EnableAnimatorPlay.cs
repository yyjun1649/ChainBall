using System;
using UnityEngine;

namespace Library
{
    public class EnableAnimatorPlay : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        public string animationName;
        
        private void OnEnable()
        {
            _animator.Play(animationName);
        }
    }
}