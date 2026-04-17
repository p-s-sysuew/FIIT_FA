using System;
using System.Collections.Generic;
using System.Diagnostics;
using Arithmetic.BigInt;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic;

internal static class Program
{
    // Здесь можно быстро менять размеры чисел.
    private static readonly int[] wordSizes = [8, 32, 128, 256, 512, 1024, 2048];

    // Сколько разных пар чисел создаём для каждого размера.
    private const int pairCount = 8;

    // Сколько раз повторяем один и тот же набор умножений.
    // Чем больше repeatCount, тем стабильнее время.
    private const int repeatCount = 3;

    private static void Main()
    {
        Console.WriteLine();

        IMultiplier simpleMultiplier = new SimpleMultiplier();
        IMultiplier karatsubaMultiplier = new KaratsubaMultiplier();
        IMultiplier fftMultiplier = new FftMultiplier();

        Random random = new Random(123456);

        foreach (int wordSize in wordSizes)
        {
            Console.WriteLine($"--- Числа с {wordSize} количеством uint-слов ---");

            List<(BetterBigInteger left, BetterBigInteger right)> testPairs =
                CreateRandomPairs(random, wordSize, pairCount);

            WarmUp(simpleMultiplier, testPairs);
            WarmUp(karatsubaMultiplier, testPairs);
            WarmUp(fftMultiplier, testPairs);

            MeasureStrategy("Simple", simpleMultiplier, testPairs, repeatCount);
            MeasureStrategy("Karatsuba", karatsubaMultiplier, testPairs, repeatCount);
            MeasureStrategy("FFT", fftMultiplier, testPairs, repeatCount);

            Console.WriteLine();
        }

        Console.WriteLine("Done.");
    }

    private static List<(BetterBigInteger left, BetterBigInteger right)> CreateRandomPairs(
        Random random,
        int wordSize,
        int count)
    {
        List<(BetterBigInteger left, BetterBigInteger right)> pairs = new List<(BetterBigInteger left, BetterBigInteger right)>(count);

        for (int i = 0; i < count; i++)
        {
            uint[] leftWords = CreateRandomWords(random, wordSize);
            uint[] rightWords = CreateRandomWords(random, wordSize);

            BetterBigInteger left = new BetterBigInteger(leftWords, false);
            BetterBigInteger right = new BetterBigInteger(rightWords, false);

            pairs.Add((left, right));
        }

        return pairs;
    }

    private static uint[] CreateRandomWords(Random random, int wordSize)
    {
        uint[] words = new uint[wordSize];

        for (int i = 0; i < wordSize; i++)
        {
            words[i] = (uint)random.NextInt64(0, 1L << 32);
        }

        if (words[wordSize - 1] == 0u)
        {
            words[wordSize - 1] = 1u;
        }

        return words;
    }

    private static void WarmUp(IMultiplier multiplier, List<(BetterBigInteger left, BetterBigInteger right)> testPairs)
    {
        foreach ((BetterBigInteger left, BetterBigInteger right) in testPairs)
        {
            _ = multiplier.Multiply(left, right);
        }
    }

    private static void MeasureStrategy(
        string title,
        IMultiplier multiplier,
        List<(BetterBigInteger left, BetterBigInteger right)> testPairs,
        int repeats)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Stopwatch stopwatch = Stopwatch.StartNew();

        int checksum = 0;

        for (int repeat = 0; repeat < repeats; repeat++)
        {
            foreach ((BetterBigInteger left, BetterBigInteger right) in testPairs)
            {
                BetterBigInteger product = multiplier.Multiply(left, right);

                checksum ^= product.GetHashCode();
            }
        }

        stopwatch.Stop();

        Console.WriteLine($"{title,-10} : {stopwatch.ElapsedMilliseconds,8} ms");
    }
}
