using System.Runtime.InteropServices;

namespace Scrinium.Core.Storage;

public static class VectorMath
{
    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        var sum = 0.0;
        foreach (var x in vector)
            sum += (double)x * x;

        var norm = Math.Sqrt(sum);
        var result = vector.ToArray();
        if (norm == 0)
            return result;

        for (var i = 0; i < result.Length; i++)
            result[i] = (float)(result[i] / norm);
        return result;
    }

    public static double Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            return double.NegativeInfinity;

        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
            sum += (double)a[i] * b[i];
        return sum;
    }

    public static byte[] ToBlob(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static float[] FromBlob(byte[] blob) => MemoryMarshal.Cast<byte, float>(blob).ToArray();
}
