using System;
using System.Collections.Generic;
using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private const int minimalSize = 32; // Порог использования классического умножения
    private const int halfWordBase = 1 << 16;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException(nameof(a), "Левый множитель не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException(nameof(b), "Правый множитель не может быть null.");
        }

        bool isNegative = a.IsNegative ^ b.IsNegative;
        uint[] res = MultiplyFft(a.GetDigits(), b.GetDigits());

        return BetterBigInteger.FromMagnitude(res, isNegative);
    }
    
    // Основная функция
    private static uint[] MultiplyFft(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] normalizedLeft = BetterBigInteger.NormalizeCopy(left);
        uint[] normalizedRight = BetterBigInteger.NormalizeCopy(right);

        int leftLength = BetterBigInteger.GetRealLength(normalizedLeft);
        int rightLength = BetterBigInteger.GetRealLength(normalizedRight);
        
        if (leftLength == 0 || rightLength == 0)
        {
            return [0u];
        }

        if (Math.Max(leftLength, rightLength) <= minimalSize)
        {
            return BetterBigInteger.MultiplyClassic(normalizedLeft, normalizedRight);
        }
        
        // Разворачиваем каждый uint на две 16-битные части
        int[] leftcoeffs = SplitWords(normalizedLeft, leftLength);
        int[] rightcoeffs = SplitWords(normalizedRight, rightLength);
        
        int neededLength = leftcoeffs.Length + rightcoeffs.Length - 1;
        
        int fftLength = 1;
        while (fftLength < neededLength)
        {
            fftLength <<= 1;
        }
        
        Complex[] leftValues = new Complex[fftLength];
        Complex[] rightValues = new Complex[fftLength];

        for (int i = 0; i < leftcoeffs.Length; i++)
        {
            leftValues[i] = new Complex(leftcoeffs[i], 0.0);
        }

        for (int i = 0; i < rightcoeffs.Length; i++)
        {
            rightValues[i] = new Complex(rightcoeffs[i], 0.0);
        }
        
        MakeFFT(leftValues, inverse: false);
        MakeFFT(rightValues, inverse: false);

        // BitReversal превращает в поэлементное умножение
        for (int i = 0; i < fftLength; i++)
        {
            leftValues[i] *= rightValues[i];
        }
        
        MakeFFT(leftValues, inverse: true);
        
        long[] rawcoeffs = new long[neededLength];
        for (int i = 0; i < neededLength; i++)
        {
            rawcoeffs[i] = (long)Math.Round(leftValues[i].Real);
        }

        // Проталкивание переносов
        long[] normalizedCoeffs = NormalizeBase(rawcoeffs);

        return PackWords(normalizedCoeffs);
    }

	// Разделение на полуслова
    private static int[] SplitWords(uint[] words, int length)
    {
        int[] coeffs = new int[length * 2];

        for (int i = 0; i < length; i++)
        {
            coeffs[2 * i] = (int)(words[i] & 0xFFFFu);
            coeffs[2 * i + 1] = (int)(words[i] >> 16);
        }

        return coeffs;
    }
    
    private static long[] NormalizeBase(long[] coeffs)
    {
        List<long> normalizedDigits = new(coeffs.Length + 4);

        long carry = 0;
        int index = 0;
        
        while (index < coeffs.Length || carry != 0)
        {
            long current = carry;

            if (index < coeffs.Length)
            {
                current += coeffs[index];
            }
            
            if (current >= 0)
            {
                long digit = current % halfWordBase;
                carry = current / halfWordBase;
                normalizedDigits.Add(digit);
            }
            else
            {
                long borrow = (-current + halfWordBase - 1) / halfWordBase;
                current += borrow * halfWordBase;
                carry = -borrow;
                normalizedDigits.Add(current);
            }
            
            index++;
        }
        
        while (normalizedDigits.Count > 1 && normalizedDigits[^1] == 0)
        {
            normalizedDigits.RemoveAt(normalizedDigits.Count - 1);
        }

        return normalizedDigits.ToArray();
    }

    private static uint[] PackWords(long[] coeffs)
    {
        if (coeffs.Length == 0)
        {
            return [0u];
        }
        
        int wordCount = (coeffs.Length + 1) / 2;
        uint[] words = new uint[wordCount];

        for (int i = 0; i < wordCount; i++)
        {
            uint lowPart = (uint)coeffs[2 * i];

            uint highPart = 0u;
            if (2 * i + 1 < coeffs.Length)
            {
                highPart = (uint)coeffs[2 * i + 1];
            }

            words[i] = lowPart | (highPart << 16);
        }

        return BetterBigInteger.NormalizeCopy(words);
    }

    private static void MakeFFT(Complex[] values, bool inverse)
    {
	    // Bit-Reversal
        int length = values.Length;
        
        for (int i = 1, j = 0; i < length; i++)
        {
            int bit = length >> 1;

            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;

            if (i < j)
            {
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        // Непосредственно FFT
        for (int blockLength = 2; blockLength <= length; blockLength <<= 1)
        {
            double angle = 2.0 * Math.PI / blockLength * (inverse ? -1.0 : 1.0);

            Complex step = new Complex(Math.Cos(angle), Math.Sin(angle));
            
            for (int blockStart = 0; blockStart < length; blockStart += blockLength)
            {
                Complex currentRoot = Complex.One;

                int half = blockLength / 2;
                
                for (int offset = 0; offset < half; offset++)
                {
                    Complex evenValue = values[blockStart + offset];
                    Complex oddValue = values[blockStart + offset + half] * currentRoot;

                    values[blockStart + offset] = evenValue + oddValue;
                    values[blockStart + offset + half] = evenValue - oddValue;

                    currentRoot *= step;
                }
            }
        }
        
        if (inverse)
        {
            for (int i = 0; i < length; i++)
            {
                values[i] /= length;
            }
        }
    }
}

// O(n log n log log n)