using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class CollisionManager : MonoBehaviour
    {

        private int _wallsCount;
        
        private CharacterMove[] _characters;
        private Renderer[] _charRenderers;
        private int _charCount;

        private float4[] _wallBounds; //x = minX, y = minY, z = maxX, w = maxY
        private float4[] _charBounds;

        private bool[] _charWallCollisions;

        private void Start()
        {
            var walls = GameObject.FindGameObjectsWithTag("Wall");
            _wallsCount = walls.Length;

            _wallBounds = walls
                .Select(wall => wall.GetComponentInChildren<Renderer>().bounds)
                .Select(bounds => new float4(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z))
                .ToArray();
            
            var charactersGo = GameObject.FindGameObjectsWithTag("Character");
            _characters = charactersGo.Select(go => go.GetComponent<CharacterMove>()).ToArray();
            _charCount = _characters.Length;
            _charRenderers = charactersGo.Select(character => character.GetComponentInChildren<Renderer>()).ToArray();
            _charBounds = new float4[_charCount];
            
            _charWallCollisions = new bool[_charCount * _wallsCount];
        }

        private void Update()
        {
            for (var i = 0; i < _charCount; ++i)
            {
                var bounds = _charRenderers[i].bounds;
                _charBounds[i] = new float4(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
            }

            for (var c = 0; c < _charCount; ++c)
            {
                for (var w = 0; w < _wallsCount; ++w)
                {
                    _charWallCollisions[c * _wallsCount + w] = CheckIntersect(_charBounds[c], _wallBounds[w]);
                }
            }

            for (var c = 0; c < _charCount; ++c)
            {
                var processed = 0;
                for (var w = 0; w < _wallsCount; ++w)
                {
                    if (_charWallCollisions[c * _wallsCount + w])
                    {
                        _characters[c].SetMoveDirection(ProcessCharWallCollision(_charBounds[c], _wallBounds[w], _characters[c].MoveDirection));
                        processed++;
                        if (processed >= 4)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private static Vector2 ProcessCharWallCollision(float4 character, float4 wall, float2 moveDirection)
        {
            float b_collision = character.w - wall.y;
            float t_collision = wall.w - character.y;
            float l_collision = character.z - wall.x;
            float r_collision = wall.z - character.x;

            if (t_collision < b_collision && t_collision < l_collision && t_collision < r_collision )
            {                           
                //Top
                return new Vector2(moveDirection.x, -moveDirection.y);
            }
            if (b_collision < t_collision && b_collision < l_collision && b_collision < r_collision)                        
            {
                //bottom
                return new Vector2(moveDirection.x, -moveDirection.y);
            }
            if (l_collision < r_collision && l_collision < t_collision && l_collision < b_collision)
            {
                //Left
                return new Vector2(-moveDirection.x, moveDirection.y);
            }
            if (r_collision < l_collision && r_collision < t_collision && r_collision < b_collision )
            {
                //Right
                return new Vector2(-moveDirection.x, moveDirection.y);
            }

            return new Vector2(-moveDirection.x, -moveDirection.y);
        }

        private static bool CheckIntersect(float4 a, float4 b)
        {
            return a.x < b.z && a.y < b.w &&
                   a.z > b.x && a.w > b.y;
        }
    }
}