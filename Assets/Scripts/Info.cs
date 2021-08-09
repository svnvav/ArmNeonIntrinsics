

using System;
using System.Linq;
using TMPro;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using Random = UnityEngine.Random;

[BurstCompile]
public class Info : MonoBehaviour
{
    [SerializeField] private TMP_Text _isNeonSupportedValue;
    [SerializeField] private TMP_Text _neonTestResultValue;
    [SerializeField] private TMP_Text _plainTestResultValue;

    private float[] _floats;
    private float _plainSum;
    
    private void Start()
    {
        var size = 1024 * 1024;
        _floats = new float[size];
        for (int i = 0; i < size; i++)
        {
            _floats[i] = Random.value;
        }

        _plainSum = _floats.Sum();
    }

    private void Update()
    {
        var sum = CalculateSumNeon(_floats, _floats.Length);
        _neonTestResultValue.text = sum.ToString();
        
        _plainTestResultValue.text = _plainSum.ToString();
        
        var usingNeon = UsingNeon();
        _isNeonSupportedValue.text = usingNeon.ToString();
    }

    private static unsafe float CalculateSumNeon(float[] a, int size)
    {
        var result = 0f;
        fixed (float* p = &a[0])
        {
            result = Test(p, size);
        }

        return result;
    }
    
    [BurstCompile]
    static bool UsingNeon()
    {
        return IsNeonSupported;
    }

    [BurstCompile]
    static unsafe float Test([NoAlias] in float* p, int size)
    {
        var result = 0f;

        if (IsNeonSupported)
        {
            v128 accum = new v128(result);
            var iterations = size / 4;
            for (int i = 0; i < iterations; ++i)
            {
                var x = vdupq_n_f32(p[i]);
                accum = vaddq_f32(accum, x);
            }
            result = accum.Float0 + accum.Float1 + accum.Float2 + accum.Float3;
        }

        return result;
    }
}
