using UnityEngine;

namespace GameYunit.Characters
{
    /// <summary>Anima somente a apresentação, a partir do deslocamento replicado. Não move o personagem.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        [SerializeField, Min(0.1f)] private float _runSpeed = 6f;
        private Animator _animator;
        private Transform _motionRoot;
        private Vector3 _previousPosition;

        private void OnEnable()
        {
            _animator = GetComponentInChildren<Animator>();
            _motionRoot = transform.parent != null ? transform.parent : transform;
            _previousPosition = _motionRoot.position;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        private void LateUpdate()
        {
            Vector3 displacement = _motionRoot.position - _previousPosition;
            _previousPosition = _motionRoot.position;
            displacement.y = 0f;
            if (_animator == null || _animator.runtimeAnimatorController == null || Time.deltaTime <= 0f)
            {
                return;
            }

            float speed = displacement.magnitude / Time.deltaTime;
            _animator.SetFloat(SpeedId, Mathf.Clamp01(speed / _runSpeed), 0.1f, Time.deltaTime);
        }
    }
}
