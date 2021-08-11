using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    public class CharacterMove : MonoBehaviour
    {
        [SerializeField] private float _startMoveSpeed = 1f;
        [SerializeField] private float _changeDirectionTime = 5f;
        
        private Vector3 _velocity;

        private float _timerToChangeDirection;

        public float2 Velocity2D => new float2(_velocity.x, _velocity.z);

        public Vector3 Velocity => _velocity;

        public Vector2 Position2D => new Vector2(transform.position.x, transform.position.z);
        
            
        private void Start()
        {
            _timerToChangeDirection = _changeDirectionTime;
            var moveDirection2D = Random.insideUnitCircle.normalized;
            SetVelocity2D(_startMoveSpeed * moveDirection2D);
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

        public void SetVelocity2D(Vector2 velocity)
        {
            _velocity = new Vector3(velocity.x, 0f, velocity.y);
        }

        public void SetVelocity(Vector3 velocity)
        {
            _velocity = velocity;
        }
        
        public void AddVelocity(Vector3 velocity)
        {
            //velocity.y = 0f;
            _velocity += velocity;
        }

        private void Move()
        {
            transform.Translate(Time.deltaTime * _velocity);
        }
    }
}
