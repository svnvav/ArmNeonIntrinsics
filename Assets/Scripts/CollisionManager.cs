using System;
using System.Diagnostics;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class CollisionManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text _info;
        
        
        private int _wallsCount;
        
        private CharacterMove[] _characters;
        private Renderer[] _charRenderers;
        private int _charCount;

        private float4[] _wallBounds; //x = minX, y = minY, z = maxX, w = maxY
        private float4[] _charBounds;
        private float2[] _charPositions;
        private float[] _charRadiuses;

        private bool[] _charWallCollisions;
        private bool[] _charCharCollisions;

        private Stopwatch _stopwatch;

        private void Start()
        {
            _stopwatch = new Stopwatch();

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
            _charPositions = new float2[_charCount];
            _charRadiuses = new float[_charCount];
            for (var i = 0; i < _charCount; i++)
            {
                _charRadiuses[i] = 0.5f;
            }
            
            _charWallCollisions = new bool[_charCount * _wallsCount];
            _charCharCollisions = new bool[_charCount * _charCount];
        }

        private void Update()
        {
            for (var i = 0; i < _charCount; ++i)
            {
                var bounds = _charRenderers[i].bounds;
                _charBounds[i] = new float4(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
                _charPositions[i] = _characters[i].Position2D;
            }

            _stopwatch.Restart();
            CheckWallChar_Plain();
            _stopwatch.Stop();
            var checkWallCharTime = _stopwatch.Elapsed;
            
            _stopwatch.Restart();
            CheckCharChar_Plain();
            _stopwatch.Stop();
            var checkCharCharTime = _stopwatch.Elapsed;

            _stopwatch.Restart();
            ResolveWallChar();
            _stopwatch.Stop();
            var resolveWallCharTime = _stopwatch.Elapsed;
            
            _stopwatch.Restart();
            ResolveCharChar();
            _stopwatch.Stop();
            var resolveCharCharTime = _stopwatch.Elapsed;
            
            _info.text = $"checkWallCharTime: {checkWallCharTime}\n" +
                         $"checkCharCharTime: {checkCharCharTime}\n" +
                         $"resolveWallCharTime: {resolveWallCharTime}\n" +
                         $"resolveCharCharTime: {resolveCharCharTime}\n";
        }

        private void ResolveWallChar()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                var processed = 0;
                for (var w = 0; w < _wallsCount; ++w)
                {
                    if (_charWallCollisions[c * _wallsCount + w])
                    {
                        var character = _characters[c];
                        var charBounds = _charBounds[c];
                        var wallBounds = _wallBounds[w];
                        
                        var bottomOverlap = charBounds.w - wallBounds.y;
                        var topOverlap = wallBounds.w - charBounds.y;
                        var leftOverlap = charBounds.z - wallBounds.x;
                        var rightOverlap = wallBounds.z - charBounds.x;

                        if (topOverlap < bottomOverlap && topOverlap < leftOverlap && topOverlap < rightOverlap)
                        {                           
                            //Top
                            character.transform.Translate(Vector3.forward * topOverlap);
                            character.SetVelocity(new Vector3(character.Velocity.x, 0f, -character.Velocity.z));
                        }
                        if (bottomOverlap < topOverlap && bottomOverlap < leftOverlap && bottomOverlap < rightOverlap)                        
                        {
                            //bottom
                            character.transform.Translate(Vector3.back * bottomOverlap);
                            character.SetVelocity(new Vector3(character.Velocity.x, 0f, -character.Velocity.z));
                        }
                        if (leftOverlap < rightOverlap && leftOverlap < topOverlap && leftOverlap < bottomOverlap)
                        {
                            //Left
                            character.transform.Translate(Vector3.left * leftOverlap);
                            character.SetVelocity(new Vector3(-character.Velocity.x, 0f, character.Velocity.z));
                        }
                        if (rightOverlap < leftOverlap && rightOverlap < topOverlap && rightOverlap < bottomOverlap)
                        {
                            //Right
                            character.transform.Translate(Vector3.right * rightOverlap);
                            character.SetVelocity(new Vector3(-character.Velocity.x, 0f, character.Velocity.z));
                        }
                        
                        processed++;
                        if (processed >= 4)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private void ResolveCharChar()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                var processed = 0;
                for (var t = c + 1; t < _charCount; ++t)
                {
                    if (_charCharCollisions[c * _charCount + t])
                    {
                        var cVelocity = _characters[c].Velocity;
                        var tVelocity = _characters[t].Velocity;
                        
                        var diff = _characters[t].transform.position - _characters[c].transform.position;
                        var dir = diff.normalized;
                        var overlapLength = diff.magnitude - (_charRadiuses[c] + _charRadiuses[t]);
                        _characters[c].transform.Translate(0.5f * overlapLength * dir);
                        _characters[t].transform.Translate(-0.5f * overlapLength * dir);

                        var tangent = Vector3.Cross(dir, Vector3.up);

                        var cVelocityNew = Vector3.Project(cVelocity, tangent);
                        var tVelocityNew = Vector3.Project(tVelocity, -tangent);
                        
                        var cVelocityToAdd = Vector3.Project(tVelocity, -dir);
                        var tVelocityToAdd = Vector3.Project(cVelocity, dir);

                        _characters[c].SetVelocity(cVelocityNew);
                        _characters[c].AddVelocity(cVelocityToAdd);
                        
                        _characters[t].SetVelocity(tVelocityNew);
                        _characters[t].AddVelocity(tVelocityToAdd);

                        processed++;
                        if (processed >= 4)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private void CheckWallChar_Plain()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                for (var w = 0; w < _wallsCount; ++w)
                {
                    _charWallCollisions[c * _wallsCount + w] = CheckAabbIntersectPlain(_charBounds[c], _wallBounds[w]);
                }
            }
        }

        private void CheckCharChar_Plain()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                for (var t = c + 1; t < _charCount; ++t)
                {
                    _charCharCollisions[c * _charCount + t] = 
                        CheckCircleIntersectPlain(_charPositions[c], _charRadiuses[c], _charPositions[t], _charRadiuses[t]);
                }
            }
        }
        
        private static bool CheckAabbIntersectPlain(float4 a, float4 b)
        {
            return a.x < b.z && a.y < b.w &&
                   a.z > b.x && a.w > b.y;
        }
        
        private static bool CheckCircleIntersectPlain(float2 positionA, float radiusA, float2 positionB, float radiusB)
        {
            return math.distancesq(positionA, positionB) < (radiusA + radiusB) * (radiusA + radiusB);
        }
        
        
    }
}