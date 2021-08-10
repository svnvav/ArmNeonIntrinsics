
using System.Diagnostics;
using TMPro;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    [BurstCompile]
    public class SumOfFloatArray : MonoBehaviour
    {
        [SerializeField] private TMP_Text _isNeonSupportedValue;
        [SerializeField] private TMP_Text _setupTime;

        [SerializeField] private TMP_Text _neonResultValue;
        [SerializeField] private TMP_Text _neonTime;
        [SerializeField] private TMP_Text _plainResultValue;
        [SerializeField] private TMP_Text _plainTime;

        private float[] _floats;
        private float _plainSum, _neonSum;

        private Stopwatch _stopwatch;


        private void Start()
        {
            var usingNeon = UsingNeon();
            _isNeonSupportedValue.text = usingNeon.ToString();

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            Setup();
            _stopwatch.Stop();
            _setupTime.text = _stopwatch.Elapsed.ToString();
        }

        public void Test()
        {
            _stopwatch.Restart();
            _plainSum = Plain(_floats);
            _stopwatch.Stop();
            _plainTime.text = _stopwatch.Elapsed.ToString();
            _plainResultValue.text = _plainSum.ToString();

            _stopwatch.Restart();
            _neonSum = Neon(_floats, _floats.Length);
            _stopwatch.Stop();
            _neonTime.text = _stopwatch.Elapsed.ToString();
            _neonResultValue.text = _neonSum.ToString();
        }

        private void Setup()
        {
            var size = 1024 * 1024;
            _floats = new float[size];
            for (int i = 0; i < size; i++)
            {
                _floats[i] = Random.value;
            }
        }

        private static float Plain(float[] floats)
        {
            float float0 = 0f, float1 = 0f, float2 = 0f, float3 = 0f;
            var chunkSize = 4;
            var iterations = floats.Length / chunkSize;
            for (int i = 0; i < iterations; i++)
            {
                float0 += floats[i * chunkSize];
                float1 += floats[i * chunkSize + 1];
                float2 += floats[i * chunkSize + 2];
                float3 += floats[i * chunkSize + 3];
            }

            return float0 + float1 + float2 + float3;
        }

        private static unsafe float Neon(float[] a, int size)
        {
            var result = 0f;
            fixed (float* p = &a[0])
            {
                result = CalculateSumOfArray_Neon(p, size);
            }

            return result;
        }

        [BurstCompile]
        private static bool UsingNeon()
        {
            return IsNeonSupported;
        }

        [BurstCompile]
        private static unsafe float CalculateSumOfArray_Neon([NoAlias] in float* p, int size)
        {
            var result = 0f;

            if (IsNeonSupported)
            {
                var accumulator = new v128(result);
                var vectorSize = 4; //float is 32 bit, accumulator has 128 = 32 * 4 bits
                var iterations = size / vectorSize;
                for (int i = 0; i < iterations; ++i)
                {
                    var x = vld1q_f32(p + i * vectorSize);
                    accumulator = vaddq_f32(accumulator, x);
                }

                result = accumulator.Float0 + accumulator.Float1 + accumulator.Float2 + accumulator.Float3;
            }

            return result;
        }
    }
}
