using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
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
        uint[] product = BetterBigInteger.MultiplyClassic(a.GetDigits(), b.GetDigits());
        
        return BetterBigInteger.FromMagnitude(product, isNegative);
    }
}

 // O(n^2)