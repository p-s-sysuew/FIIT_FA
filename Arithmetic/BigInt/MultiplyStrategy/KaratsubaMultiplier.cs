using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int minimalSize = 32; // Порог использования классического умножения
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b) 
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый множитель не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый множитель не может быть null.");
        }

        bool isNegative = a.IsNegative ^ b.IsNegative;
        uint[] res = MultiplyKaratsuba(a.GetDigits(), b.GetDigits());

        return BetterBigInteger.FromMagnitude(res, isNegative);
    }

    private static uint[] MultiplyKaratsuba(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] normalizedLeft = BetterBigInteger.NormalizeCopy(left);
        uint[] normalizedRight = BetterBigInteger.NormalizeCopy(right);

        int leftLength = BetterBigInteger.GetRealLength(normalizedLeft);
        int rightLength = BetterBigInteger.GetRealLength(normalizedRight);

        if (leftLength == 0 || rightLength == 0)
        {
            return [0u];
        }

        int size = Math.Max(leftLength, rightLength);

        if (size <= minimalSize)
        {
            return BetterBigInteger.MultiplyClassic(normalizedLeft, normalizedRight);
        }

        int half = size / 2;

        ReadOnlySpan<uint> leftLow = normalizedLeft.AsSpan(0, Math.Min(half, leftLength));
        ReadOnlySpan<uint> leftHigh = normalizedLeft.AsSpan(Math.Min(half, leftLength), leftLength - Math.Min(half, leftLength));
        ReadOnlySpan<uint> rightLow = normalizedRight.AsSpan(0, Math.Min(half, rightLength));
        ReadOnlySpan<uint> rightHigh = normalizedRight.AsSpan(Math.Min(half, rightLength), rightLength - Math.Min(half, rightLength));
        
        // z0 = x0 * y0
        uint[] z0 = MultiplyKaratsuba(leftLow, rightLow);

        // z2 = x1 * y1
        uint[] z2 = MultiplyKaratsuba(leftHigh, rightHigh);

        // z1 = (x0 + x1) * (y0 + y1)
        uint[] leftSum = BetterBigInteger.AddMagnitudes(leftLow, leftHigh);
        uint[] rightSum = BetterBigInteger.AddMagnitudes(rightLow, rightHigh);
        uint[] z1 = MultiplyKaratsuba(leftSum, rightSum);

        // z1 = z1 - z0 - z2
        z1 = BetterBigInteger.SubtractMagnitudes(z1, z0);
        z1 = BetterBigInteger.SubtractMagnitudes(z1, z2);

        uint[] middlePart = BetterBigInteger.ShiftByWordsLeft(z1, half);
        uint[] highPart = BetterBigInteger.ShiftByWordsLeft(z2, 2 * half);

        uint[] result = BetterBigInteger.AddMagnitudes(z0, middlePart);
        result = BetterBigInteger.AddMagnitudes(result, highPart);

        return result;
    }
}

// O(n^1.585)