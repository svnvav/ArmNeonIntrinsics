using System;
using System.Diagnostics;
using System.Linq;
using TMPro;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using Debug = UnityEngine.Debug;

namespace DefaultNamespace
{
    [BurstCompile]
    public class CollisionManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text _isNeonSupportedValue;
        [SerializeField] private TMP_Text _info;


        private int _wallCount;

        private CharacterMove[] _characters;
        private Renderer[] _charRenderers;
        private int _charCount;

        private float4[] _wallBounds; //x = minX, y = minY, z = -maxX, w = -maxY
        private float[] _wallBounds_floats;
        private float[] _wallBounds_unrolled;
        private float4[] _charBounds; //x = maxX , y = maxY, z = -minX, w = -minY
        private float[] _charBounds_floats;
        private float[] _charBounds_unrolled;
        private float[] _charPositionsX, _charPositionsY;
        private float[] _charRadii; //Да, во множественном числе слово "радиусы" переводится как radii

        private bool[] _wallCharCollisions;
        private bool[] _charCharCollisions;

        private Stopwatch _stopwatch;

        private void Start()
        {
            var usingNeon = UsingNeon();
            _isNeonSupportedValue.text = usingNeon.ToString();

            _stopwatch = new Stopwatch();

            var walls = GameObject.FindGameObjectsWithTag("Wall");
            _wallCount = walls.Length;

            _wallBounds = walls
                .Select(wall => wall.GetComponentInChildren<Renderer>().bounds)
                .Select(bounds => new float4(bounds.min.x, bounds.min.z, -bounds.max.x, -bounds.max.z))
                .ToArray();

            _wallBounds_floats = new float[_wallCount * 4];
            _wallBounds_unrolled = new float[_wallCount * 4];
            for (int i = 0; i < _wallCount; ++i)
            {
                _wallBounds_floats[i * 4 + 0] = _wallBounds[i].x;
                _wallBounds_floats[i * 4 + 1] = _wallBounds[i].y;
                _wallBounds_floats[i * 4 + 2] = _wallBounds[i].z;
                _wallBounds_floats[i * 4 + 3] = _wallBounds[i].w;

                _wallBounds_unrolled[0 * _wallCount + i] = _wallBounds[i].x;
                _wallBounds_unrolled[1 * _wallCount + i] = _wallBounds[i].y;
                _wallBounds_unrolled[2 * _wallCount + i] = _wallBounds[i].z;
                _wallBounds_unrolled[3 * _wallCount + i] = _wallBounds[i].w;
            }

            var charactersGo = GameObject.FindGameObjectsWithTag("Character");
            _characters = charactersGo.Select(go => go.GetComponent<CharacterMove>()).ToArray();
            _charCount = _characters.Length;

            _charRenderers = charactersGo.Select(character => character.GetComponentInChildren<Renderer>()).ToArray();
            _charBounds = new float4[_charCount];
            _charBounds_floats = new float[_charCount * 4];
            _charBounds_unrolled = new float[_charCount * 4];

            _charPositionsX = new float[_charCount];
            _charPositionsY = new float[_charCount];
            _charRadii = new float[_charCount];
            for (var i = 0; i < _charCount; i++)
            {
                _charRadii[i] = 0.5f;
            }

            _wallCharCollisions = new bool[_charCount * _wallCount];
            _charCharCollisions = new bool[_charCount * _charCount];
        }

        private void Update()
        {
            for (var i = 0; i < _charCount; ++i)
            {
                var bounds = _charRenderers[i].bounds;
                _charBounds[i] = new float4(bounds.max.x, bounds.max.z, -bounds.min.x, -bounds.min.z);
                _charPositionsX[i] = _characters[i].transform.position.x;
                _charPositionsY[i] = _characters[i].transform.position.z;

                _charBounds_floats[i * 4 + 0] = _charBounds[i].x;
                _charBounds_floats[i * 4 + 1] = _charBounds[i].y;
                _charBounds_floats[i * 4 + 2] = _charBounds[i].z;
                _charBounds_floats[i * 4 + 3] = _charBounds[i].w;

                _charBounds_unrolled[0 * _charCount + i] = _charBounds[i].x;
                _charBounds_unrolled[1 * _charCount + i] = _charBounds[i].y;
                _charBounds_unrolled[2 * _charCount + i] = _charBounds[i].z;
                _charBounds_unrolled[3 * _charCount + i] = _charBounds[i].w;
            }

            _stopwatch.Restart();
            //CheckWallChar_Plain();
            _stopwatch.Stop();
            var checkWallCharTime_Plain = _stopwatch.Elapsed;

            _stopwatch.Restart();
            //CheckWallChar_Neon();
            _stopwatch.Stop();
            var checkWallCharTime_Neon = _stopwatch.Elapsed;

            _stopwatch.Restart();
            CheckWallChar_NeonUnrolled();
            _stopwatch.Stop();
            var checkWallCharTime_NeonUnrolled = _stopwatch.Elapsed;

            _stopwatch.Restart();
            //CheckCharChar_Plain();
            _stopwatch.Stop();
            var checkCharCharTime_Plain = _stopwatch.Elapsed;

            _stopwatch.Restart();
            CheckCharChar_Neon();
            _stopwatch.Stop();
            var checkCharCharTime_Neon = _stopwatch.Elapsed;

            _stopwatch.Restart();
            ResolveWallChar();
            _stopwatch.Stop();
            var resolveWallCharTime = _stopwatch.Elapsed;

            _stopwatch.Restart();
            ResolveCharChar();
            _stopwatch.Stop();
            var resolveCharCharTime = _stopwatch.Elapsed;

            _info.text = $"checkWallChar_Plain: {checkWallCharTime_Plain}\n" +
                         $"checkWallChar_Neon: {checkWallCharTime_Neon}\n" +
                         $"checkWallChar_NeonUnrolled: {checkWallCharTime_NeonUnrolled}\n" +
                         $"checkCharChar_Plain: {checkCharCharTime_Plain}\n" +
                         $"checkCharChar_Neon: {checkCharCharTime_Neon}\n" +
                         $"resolveWallChar: {resolveWallCharTime}\n" +
                         $"resolveCharChar: {resolveCharCharTime}\n";
        }

        private void ResolveWallChar()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                var processed = 0;
                for (var w = 0; w < _wallCount; ++w)
                {
                    if (_wallCharCollisions[c * _wallCount + w])
                    {
                        var character = _characters[c];
                        var charBounds = _charBounds[c];
                        var wallBounds = _wallBounds[w];

                        var bottomOverlap = charBounds.y - wallBounds.y;
                        var topOverlap = charBounds.w - wallBounds.w;
                        var leftOverlap = charBounds.x - wallBounds.x;
                        var rightOverlap = charBounds.z - wallBounds.z;

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
                        var overlapLength = diff.magnitude - (_charRadii[c] + _charRadii[t]);
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
                for (var w = 0; w < _wallCount; ++w)
                {
                    _wallCharCollisions[c * _wallCount + w] = CheckAabbIntersect(_charBounds[c], _wallBounds[w]);
                }
            }
        }

        private unsafe void CheckWallChar_Neon()
        {
            fixed (float* charBounds = _charBounds_floats, wallBounds = _wallBounds_floats)
            fixed (bool* collisions = _wallCharCollisions)
            {
                ProcessCheckWallChar_Neon(charBounds, _charCount, wallBounds, _wallCount, collisions);
            }
        }

        private unsafe void CheckWallChar_NeonUnrolled()
        {
            fixed (float* charBounds = _charBounds_unrolled, wallBounds = _wallBounds_unrolled)
            fixed (bool* collisions = _wallCharCollisions)
            {
                ProcessCheckWallChar_NeonUnrolled(charBounds, _charCount, wallBounds, _wallCount, collisions);
            }
        }

        private void CheckCharChar_Plain()
        {
            for (var c = 0; c < _charCount; ++c)
            {
                for (var t = c + 1; t < _charCount; ++t)
                {
                    _charCharCollisions[c * _charCount + t] = CheckCircleIntersect(
                        _charPositionsX[c],
                        _charPositionsY[c],
                        _charRadii[c],
                        _charPositionsX[t],
                        _charPositionsY[t],
                        _charRadii[t]
                    );
                }
            }
        }

        private unsafe void CheckCharChar_Neon()
        {
            fixed (float* charPositionsX = _charPositionsX, charPositionsY = _charPositionsY, charRadii = _charRadii)
            fixed (bool* collisions = _charCharCollisions)
            {
                ProcessCheckCharChar_Neon(charPositionsX, charPositionsY, charRadii, _charCount, collisions);
            }
        }

        [BurstCompile]
        private static bool UsingNeon()
        {
            return IsNeonSupported;
        }

        [BurstCompile]
        private static unsafe void ProcessCheckWallChar_Neon(
            [NoAlias] in float* charBounds, int charCount,
            [NoAlias] in float* wallBounds, int wallCount,
            [NoAlias] bool* collisions
        )
        {
            if (!IsNeonSupported) return;

            for (var c = 0; c < charCount; ++c)
            {
                var charBoundsVector = vld1q_f32(charBounds + 4 * c);
                for (int w = 0; w < wallCount; w++)
                {
                    var wallBoundsVector = vld1q_f32(wallBounds + 4 * w);
                    collisions[c * wallCount + w] = vmaxvq_u32(vcgeq_f32(wallBoundsVector, charBoundsVector)) == 0;
                }
            }
        }

        [BurstCompile]
        private static unsafe void ProcessCheckWallChar_NeonUnrolled(
            [NoAlias] in float* charBounds, int charCount,
            [NoAlias] in float* wallBounds, int wallCount,
            [NoAlias] bool* collisions
        )
        {
            if (!IsNeonSupported) return;

            var lookup1 = new v64(0, 4, 8, 12, 255, 255, 255, 255);
            var lookup2 = new v64(255, 255, 255, 255, 0, 4, 8, 12);

            for (var c = 0; c < charCount; ++c)
            {
                var charMaxX = vdupq_n_f32(*(charBounds + 0 * charCount + c));
                var charMaxY = vdupq_n_f32(*(charBounds + 1 * charCount + c));
                var charMinX = vdupq_n_f32(*(charBounds + 2 * charCount + c));
                var charMinY = vdupq_n_f32(*(charBounds + 3 * charCount + c));
                var w = 0;
                for (; w < (wallCount & ~7);)
                {
                    var wallsMinX = vld1q_f32(wallBounds + 0 * wallCount + w);
                    var wallsMinY = vld1q_f32(wallBounds + 1 * wallCount + w);
                    var wallsMaxX = vld1q_f32(wallBounds + 2 * wallCount + w);
                    var wallsMaxY = vld1q_f32(wallBounds + 3 * wallCount + w);

                    var result = vqtbl1_u8(
                        vorrq_u32(
                            vorrq_u32(vcgeq_f32(wallsMinX, charMaxX), vcgeq_f32(wallsMaxX, charMinX)),
                            vorrq_u32(vcgeq_f32(wallsMinY, charMaxY), vcgeq_f32(wallsMaxY, charMinY))
                        ),
                        lookup1
                    );

                    w += 4;
                    
                    wallsMinX = vld1q_f32(wallBounds + 0 * wallCount + w);
                    wallsMinY = vld1q_f32(wallBounds + 1 * wallCount + w);
                    wallsMaxX = vld1q_f32(wallBounds + 2 * wallCount + w);
                    wallsMaxY = vld1q_f32(wallBounds + 3 * wallCount + w);
                    
                    result = vqtbx1_u8(
                        result,
                        vorrq_u32(
                            vorrq_u32(vcgeq_f32(wallsMinX, charMaxX), vcgeq_f32(wallsMaxX, charMinX)),
                            vorrq_u32(vcgeq_f32(wallsMinY, charMaxY), vcgeq_f32(wallsMaxY, charMinY))
                        ),
                        lookup2
                    );

                    *(v64*) (collisions + c * wallCount + w - 4) = vmvn_u8(result);
                    
                    w += 4;
                }
            }
        }

        [BurstCompile]
        private static unsafe void ProcessCheckCharChar_Neon(
            [NoAlias] in float* positionsX,
            [NoAlias] in float* positionsY,
            [NoAlias] in float* radii,
            int charCount,
            [NoAlias] bool* collisions
        )
        {
            if (!IsNeonSupported) return;

            for (var c = 0; c < charCount; ++c)
            {
                var charPosX = vdupq_n_f32(positionsX[c]);
                var charPosY = vdupq_n_f32(positionsY[c]);
                var charRadius = vdupq_n_f32(radii[c]);
                int t = c + 1;
                for (; t < charCount - c % 4; t += 4)
                {
                    var targetPosXVector = vld1q_f32(positionsX + t);
                    var targetPosYVector = vld1q_f32(positionsY + t);
                    var targetRadiusVector = vld1q_f32(radii + t);

                    var squareDistance = vaddq_f32(
                        vmulq_f32(vsubq_f32(charPosX, targetPosXVector), vsubq_f32(charPosX, targetPosXVector)),
                        vmulq_f32(vsubq_f32(charPosY, targetPosYVector), vsubq_f32(charPosY, targetPosYVector))
                    );

                    //(charRadius + targetRadius)^2
                    var radSumSqr = vmulq_f32(vaddq_f32(charRadius, targetRadiusVector), vaddq_f32(charRadius, targetRadiusVector));

                    var comparison = vcltq_f32(squareDistance, radSumSqr);

                    collisions[c * charCount + t + 0] = vgetq_lane_u32(comparison, 0) > 0;
                    collisions[c * charCount + t + 1] = vgetq_lane_u32(comparison, 1) > 0;
                    collisions[c * charCount + t + 2] = vgetq_lane_u32(comparison, 2) > 0;
                    collisions[c * charCount + t + 3] = vgetq_lane_u32(comparison, 3) > 0;
                }

                while (t < charCount)
                {
                    /*var squareDistance =
                        (positionsX[c] - positionsX[t]) * (positionsX[c] - positionsX[t]) +
                        (positionsY[c] - positionsY[t]) * (positionsY[c] - positionsY[t]);
                    var radSumSqr = (radii[c] + radii[t]) * (radii[c] + radii[t]);*/
                    collisions[c * charCount + t] = CheckCircleIntersect(
                        positionsX[c],
                        positionsY[c],
                        radii[c],
                        positionsX[t],
                        positionsY[t],
                        radii[t]
                    );
                    t++;
                }
            }
        }

        private static bool CheckAabbIntersect(float4 character, float4 wall)
        {
            return character.z > wall.z && character.w > wall.w &&
                   character.x > wall.x && character.y > wall.y;
        }

        private static bool CheckCircleIntersect(float positionAX, float positionAY, float radiusA, float positionBX, float positionBY, float radiusB)
        {
            return (positionAX - positionBX) * (positionAX - positionBX) + (positionAY - positionBY) * (positionAY - positionBY) <
                   (radiusA + radiusB) * (radiusA + radiusB);
        }
    }
}