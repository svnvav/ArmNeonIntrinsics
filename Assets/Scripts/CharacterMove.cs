using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    public class CharacterMove : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 1f;
        [SerializeField] private float _changeDirectionTime = 5f;
        
        private Vector3 _moveDirection;

        private float _timerToChangeDirection;

        public float2 MoveDirection => new float2(_moveDirection.x, _moveDirection.z);

        private void Start()
        {
            _timerToChangeDirection = _changeDirectionTime;
            var moveDirection2D = Random.insideUnitCircle.normalized;
            SetMoveDirection(moveDirection2D);
        }

        private void Update()
        {
            /*_timerToChangeDirection += Time.deltaTime;
            while (_timerToChangeDirection > _changeDirectionTime)
            {
                ChangeDirection();
                _timerToChangeDirection -= _changeDirectionTime;
            }*/

            Move();
        }

        public void SetMoveDirection(Vector2 moveDirection2D)
        {
            _moveDirection = new Vector3(moveDirection2D.x, 0f, moveDirection2D.y);
        }

        private void Move()
        {
            transform.Translate(Time.deltaTime * _moveSpeed * _moveDirection);
        }
    }
}
