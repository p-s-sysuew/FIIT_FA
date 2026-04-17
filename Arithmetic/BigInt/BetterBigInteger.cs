using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

// TEST ME!
// dotnet test Arithmetic.Tests/Arithmetic.Tests.csproj

public sealed class BetterBigInteger : IBigInteger
{
    private const int bitsPerWord = 32;

    // Пороги выбора алгоритма умножения
    private const int SimpleMultiplierZone = 32;
    private const int KaratsubaZone = 128;
    
    private static readonly IMultiplier simpleMultiplierStrategy = new SimpleMultiplier();
    private static readonly IMultiplier karatsubaMultiplierStrategy = new KaratsubaMultiplier();
    private static readonly IMultiplier fftMultiplierStrategy = new FftMultiplier();
    
    private int _signBit;
    
    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    
    private uint[]? _data;
    
    /// Конструктор нуля
    private BetterBigInteger()
    {
        _signBit = 0;
        _smallValue = 0;
        _data = null;
    }

    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        if (digits is null)
        {
            throw new ArgumentNullException("Массив цифр не может быть null.");
        }

        StoreDigits(digits, isNegative);
    }

    /// Конструктор от любого перечисления слов
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        if (digits is null)
        {
            throw new ArgumentNullException("Набор цифр не может быть null.");
        }

        StoreDigits(digits.ToArray(), isNegative);
    }

    /// Конструктор от строки в произвольном основании от 2 до 36
    public BetterBigInteger(string value, int radix)
    {
        if (value is null)
        {
            throw new ArgumentNullException("Строка не может быть null.");
        }

        if (radix < 2 || radix > 36)
        {
            throw new ArgumentOutOfRangeException("Основание системы счисления должно быть от 2 до 36.");
        }

        string text = value.Trim();

        if (text.Length == 0)
        {
            throw new FormatException("Пустая строка не является корректным числом.");
        }

        bool isNegative = false;
        int startIndex = 0;

        if (text[0] == '+')
        {
            startIndex = 1;
        }
        else if (text[0] == '-')
        {
            isNegative = true;
            startIndex = 1;
        }

        if (startIndex >= text.Length)
        {
            throw new FormatException("После знака в строке нет цифр.");
        }

        // Схема горнера
        uint[] magnitude = [0u];

        for (int i = startIndex; i < text.Length; i++)
        {
            int digit = ParseDigit(text[i]);

            if (digit < 0 || digit >= radix)
            {
                throw new FormatException($"Символ в строке не подходит для системы счисления с текущим основанием.");
            }
            
            magnitude = MultiplyByUInt(magnitude, (uint)radix);
            magnitude = AddUInt(magnitude, (uint)digit);
        }
        
        StoreDigits(magnitude, isNegative);
    }
    
    public bool IsNegative => _signBit == 1 && !IsZero;
    
    private bool IsZero => _data is null && _smallValue == 0u;
    
    private int WordCount => _data?.Length ?? 1;
    
    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }
    
    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (IsNegative != other.IsNegative)
        {
            return IsNegative ? -1 : 1;
        }
        
        int biggerMagnitude = CompareMagnitudes(GetDigits(), other.GetDigits());
        return IsNegative ? -biggerMagnitude : biggerMagnitude;
    }
    
    public bool Equals(IBigInteger? other)
    {
        return CompareTo(other) == 0;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is IBigInteger other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(IsNegative);

        ReadOnlySpan<uint> digits = GetDigits();
        int length = GetRealLength(digits);

        if (length == 0)
        {
            hash.Add(0u);
            return hash.ToHashCode();
        }

        for (int i = 0; i < length; i++)
        {
            hash.Add(digits[i]);
        }

        return hash.ToHashCode();
    }
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        ReadOnlySpan<uint> left = a.GetDigits();
        ReadOnlySpan<uint> right = b.GetDigits();
        
        if (a.IsNegative == b.IsNegative)
        {
            return FromMagnitude(AddMagnitudes(left, right), a.IsNegative);
        }
        
        int comparison = CompareMagnitudes(left, right);
        
        if (comparison == 0)
        {
            return Zero();
        }
        
        if (comparison > 0)
        {
            return FromMagnitude(SubtractMagnitudes(left, right), a.IsNegative);
        }

        return FromMagnitude(SubtractMagnitudes(right, left), b.IsNegative);
    }
    
    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        return a + (-b);
    }
    
    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Операнд не может быть null.");
        }

        if (a.IsZero)
        {
            return Zero();
        }

        return FromMagnitude(a.GetDigits(), !a.IsNegative);
    }
    
    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        if (b.IsZero)
        {
            throw new DivideByZeroException("Деление на ноль запрещено.");
        }

        uint[] divRes = DivMagnitudes(a.GetDigits(), b.GetDigits(), out _);
        bool isNegative = (a.IsNegative ^ b.IsNegative) && !IsMagnitudeZero(divRes);
        return FromMagnitude(divRes, isNegative);
    }
    
    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        if (b.IsZero)
        {
            throw new DivideByZeroException("Остаток по модулю нуля не определен.");
        }
        
        DivMagnitudes(a.GetDigits(), b.GetDigits(), out uint[] remainder);
        bool isNegative = a.IsNegative && !IsMagnitudeZero(remainder);
        return FromMagnitude(remainder, isNegative);
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        int size = Math.Max(a.WordCount, b.WordCount);

        IMultiplier strategy = size < SimpleMultiplierZone ? simpleMultiplierStrategy : size < KaratsubaZone ? karatsubaMultiplierStrategy : fftMultiplierStrategy;
        
        return strategy.Multiply(a, b);
    }
    
    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Операнд не может быть null.");
        }

        int targetWordCount = Math.Max(1, a.WordCount + 1);
        uint[] words = ToBinaryView(a, targetWordCount);

        for (int i = 0; i < words.Length; i++)
        {
            words[i] = ~words[i];
        }
        
        return FromBinaryView(words);
    }
    
    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        return CompleteBinaryOperation(a, b, static (leftWord, rightWord) => leftWord & rightWord);
    }
    
    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        return CompleteBinaryOperation(a, b, static (leftWord, rightWord) => leftWord | rightWord);
    }
    
    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        return CompleteBinaryOperation(a, b, static (leftWord, rightWord) => leftWord ^ rightWord);
    }
    
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Операнд не может быть null.");
        }

        if (shift == int.MinValue)
        {
            throw new ArgumentOutOfRangeException("Невозможно инвертировать минимальный int: возможно переполнение памяти.");
        }
        
        if (shift < 0)
        {
            return a >> -shift;
        }
        
        if (shift == 0 || a.IsZero)
        {
            return FromMagnitude(a.GetDigits(), a.IsNegative);
        }

        return FromMagnitude(ShiftLeftMagnitude(a.GetDigits(), shift), a.IsNegative);
    }
    
    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Операнд не может быть null.");
        }

        if (shift == int.MinValue)
        {
            throw new ArgumentOutOfRangeException("Невозможно инвертировать минимальный int: возможно переполнение памяти.");
        }

        if (shift < 0)
        {
            return a << -shift;
        }

        if (shift == 0 || a.IsZero)
        {
            return FromMagnitude(a.GetDigits(), a.IsNegative);
        }
        
        if (!a.IsNegative)
        {
            return FromMagnitude(ShiftRightMagnitude(a.GetDigits(), shift), false);
        }
        
        // Для отрицательного числа (чтобы обеспечить округление вниз):
        // (-x) >> k = -((x + (2^k - 1)) >> k)
        BetterBigInteger one = FromMagnitude([1u], false);
        BetterBigInteger temp = (one << shift) - one;
        BetterBigInteger adjusted = Abs(a) + temp;
        
        BetterBigInteger shiftedMagnitude = FromMagnitude(ShiftRightMagnitude(adjusted.GetDigits(), shift), false);
        return shiftedMagnitude.IsZero ? Zero() : -shiftedMagnitude;
    }

    public static bool operator ==(BetterBigInteger? a, BetterBigInteger? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(BetterBigInteger? a, BetterBigInteger? b)
    {
        return !(a == b);
    }

    public static bool operator <(BetterBigInteger a, BetterBigInteger b)
    {
        return a.CompareTo(b) < 0;
    }

    public static bool operator >(BetterBigInteger a, BetterBigInteger b)
    {
        return a.CompareTo(b) > 0;
    }

    public static bool operator <=(BetterBigInteger a, BetterBigInteger b)
    {
        return a.CompareTo(b) <= 0;
    }

    public static bool operator >=(BetterBigInteger a, BetterBigInteger b)
    {
        return a.CompareTo(b) >= 0;
    }

    public override string ToString()
    {
        return ToString(10);
    }
    
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
        {
            throw new ArgumentOutOfRangeException("Основание системы счисления должно быть от 2 до 36.");
        }

        if (IsZero)
        {
            return "0";
        }

        uint[] work = NormalizeCopy(GetDigits());
        int length = work.Length;
        StringBuilder reversedDigits = new();
        
        // As usual.
        while (length > 0)
        {
            uint remainder = DivSmall(work, ref length, (uint)radix);
            reversedDigits.Append(DigitToChar((int)remainder));
        }

        if (IsNegative)
        {
            reversedDigits.Append('-');
        }

        char[] chars = reversedDigits.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
    
    #region HelperFuncs
    
    internal static BetterBigInteger FromMagnitude(ReadOnlySpan<uint> digits, bool isNegative)
    {
        return new BetterBigInteger(digits.ToArray(), isNegative);
    }
    
    internal static bool IsMagnitudeZero(ReadOnlySpan<uint> digits)
    {
        return GetRealLength(digits) == 0;
    }
    
    internal static int GetRealLength(ReadOnlySpan<uint> digits)
    {
        int length = digits.Length;

        while (length > 0 && digits[length - 1] == 0u)
        {
            length--;
        }

        return length;
    }
    
    internal static uint[] NormalizeCopy(ReadOnlySpan<uint> digits)
    {
        int length = GetRealLength(digits);

        if (length == 0)
        {
            return [0u];
        }

        uint[] result = new uint[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = digits[i];
        }

        return result;
    }
    
    internal static int CompareMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int leftLength = GetRealLength(left);
        int rightLength = GetRealLength(right);

        if (leftLength != rightLength)
        {
            return leftLength < rightLength ? -1 : 1;
        }
        
        for (int i = leftLength - 1; i >= 0; i--)
        {
            if (left[i] != right[i])
            {
                return left[i] < right[i] ? -1 : 1;
            }
        }

        return 0;
    }
    
	// Сложение слов   
    private static uint Add32(uint left, uint right, uint accIn, out uint accOut)
    {
        uint sum = left + right;
        uint acc1 = sum < left ? 1u : 0u;

        uint result = sum + accIn;
        uint acc2 = result < sum ? 1u : 0u;

        accOut = acc1 + acc2;
        return result;
    }
    
    private static void AddToWordArray(uint[] result, int index, uint value)
    {
        uint acc = value;
        int currentIndex = index;

        while (acc != 0)
        {
            uint sum = result[currentIndex] + acc;
            acc = sum < result[currentIndex] ? 1u : 0u;
            result[currentIndex] = sum;
            currentIndex++;
        }
    }

    // Умножение слов
    private static void Multiply32(uint left, uint right, out uint low, out uint high)
    {
        uint leftLow = left & 0xFFFFu;
        uint leftHigh = left >> 16;
        uint rightLow = right & 0xFFFFu;
        uint rightHigh = right >> 16;

        uint part00 = leftLow * rightLow;
        uint part01 = leftLow * rightHigh;
        uint part10 = leftHigh * rightLow;
        uint part11 = leftHigh * rightHigh;
        
        // Середина moment
        uint accFromMiddle = 0;
        uint middle = Add32(part00 >> 16, part01 & 0xFFFFu, 0u, out accFromMiddle);
        uint middleAcc = 0;
        middle = Add32(middle, part10 & 0xFFFFu, 0u, out middleAcc);
        
        // Формирование младшей части
        low = (part00 & 0xFFFFu) | ((middle & 0xFFFFu) << 16);
        
        // Формирование старшей части
        uint highacc = accFromMiddle + middleAcc;
        high = part11;
        high += (part01 >> 16);
        high += (part10 >> 16);
        high += (middle >> 16);
        high += highacc;
    }
    
    internal static uint[] AddMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int maxLength = Math.Max(left.Length, right.Length);
        uint[] result = new uint[maxLength + 1];
        uint acc = 0;

        for (int i = 0; i < maxLength; i++)
        {
            uint leftValue = i < left.Length ? left[i] : 0u;
            uint rightValue = i < right.Length ? right[i] : 0u;
            result[i] = Add32(leftValue, rightValue, acc, out acc);
        }

        result[maxLength] = acc;
        return NormalizeCopy(result);
    }
    
    internal static uint[] SubtractMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        if (CompareMagnitudes(left, right) < 0)
        {
            throw new ArgumentException("Неверное использование функции SubtractMagnitudes.");
        }

        uint[] result = new uint[left.Length];
        long acc = 0;

        for (int i = 0; i < left.Length; i++)
        {
            long current = (long)left[i] - acc;

            if (i < right.Length)
            {
                current -= right[i];
            }

            if (current < 0)
            {
                current += 1L << bitsPerWord;
                acc = 1;
            }
            else
            {
                acc = 0;
            }

            result[i] = (uint)current;
        }

        return NormalizeCopy(result);
    }
    
    internal static uint[] MultiplyClassic(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int leftLength = GetRealLength(left);
        int rightLength = GetRealLength(right);

        if (leftLength == 0 || rightLength == 0)
        {
            return [0u];
        }

        uint[] result = new uint[leftLength + rightLength];

        for (int i = 0; i < leftLength; i++)
        {
            uint acc = 0;

            for (int j = 0; j < rightLength; j++)
            {
                Multiply32(left[i], right[j], out uint productLow, out uint productHigh);

                uint sum = Add32(result[i + j], productLow, acc, out uint accFromLow);
                result[i + j] = sum;

                uint nextacc = productHigh + accFromLow;
                if (nextacc < productHigh)
                {
                    AddToWordArray(result, i + j + 2, 1u);
                }

                acc = nextacc;
            }

            AddToWordArray(result, i + rightLength, acc);
        }

        return NormalizeCopy(result);
    }
    
    internal static uint[] ShiftByWordsLeft(ReadOnlySpan<uint> digits, int wordShift)
    {
        if (wordShift < 0)
        {
            throw new ArgumentOutOfRangeException("Количество слов для сдвига не может быть отрицательным.");
        }

        int length = GetRealLength(digits);

        if (length == 0)
        {
            return [0u];
        }

        uint[] result = new uint[length + wordShift];

        for (int i = 0; i < length; i++)
        {
            result[i + wordShift] = digits[i];
        }

        return result;
    }

    private static BetterBigInteger Zero()
    {
        return new BetterBigInteger();
    }
    
    private static BetterBigInteger Abs(BetterBigInteger value)
    {
        return FromMagnitude(value.GetDigits(), false);
    }
    
    private void StoreDigits(uint[] digits, bool isNegative)
    {
        if (digits is null)
        {
            throw new ArgumentNullException("Массив цифр не может быть null.");
        }

        int length = GetRealLength(digits);
        
        if (length == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
            return;
        }
        
        if (length == 1)
        {
            _signBit = digits[0] == 0u ? 0 : (isNegative ? 1 : 0);
            _smallValue = digits[0];
            _data = null;
            return;
        }
        
        _signBit = isNegative ? 1 : 0;
        _smallValue = 0;
        _data = new uint[length];
        Array.Copy(digits, _data, length);
    }
    
    private static int ParseDigit(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'A' && c <= 'Z')
        {
            return c - 'A' + 10;
        }

        if (c >= 'a' && c <= 'z')
        {
            return c - 'a' + 10;
        }

        return -1;
    }
    
    private static char DigitToChar(int digit)
    {
        return digit < 10 ? (char)('0' + digit) : (char)('A' + digit - 10);
    }
    
    // Микроприбавление
    private static uint[] AddUInt(ReadOnlySpan<uint> digits, uint value)
    {
        if (value == 0)
        {
            return NormalizeCopy(digits);
        }

        int length = GetRealLength(digits);

        if (length == 0)
        {
            return [value];
        }

        uint[] result = new uint[length + 1];
        uint acc = value;
        int index = 0;

        while (index < length)
        {
            uint sum = digits[index] + acc;
            result[index] = sum;
            acc = sum < digits[index] ? 1u : 0u;
            index++;

            if (acc == 0)
            {
                for (int copy = index; copy < length; copy++)
                {
                    result[copy] = digits[copy];
                }

                return NormalizeCopy(result);
            }
        }

        result[length] = acc;
        return NormalizeCopy(result);
    }
    
    // Микроумножение
    private static uint[] MultiplyByUInt(ReadOnlySpan<uint> digits, uint factor)
    {
        int length = GetRealLength(digits);

        if (length == 0 || factor == 0u)
        {
            return [0u];
        }

        if (factor == 1u)
        {
            return NormalizeCopy(digits);
        }

        uint[] result = new uint[length + 1];
        uint acc = 0;

        for (int i = 0; i < length; i++)
        {
            Multiply32(digits[i], factor, out uint productLow, out uint productHigh);
            result[i] = Add32(productLow, acc, 0u, out uint accFromLow);
            acc = productHigh + accFromLow;
        }

        result[length] = acc;
        return NormalizeCopy(result);
    }
    
    private static uint[] ShiftLeftMagnitude(ReadOnlySpan<uint> digits, int shift)
    {
        int length = GetRealLength(digits);

        if (length == 0 || shift == 0)
        {
            return NormalizeCopy(digits);
        }

        int wordShift = shift / bitsPerWord;
        int bitShift = shift % bitsPerWord;
        uint[] result = new uint[length + wordShift + 1];

        if (bitShift == 0)
        {
            for (int i = 0; i < length; i++)
            {
                result[i + wordShift] = digits[i];
            }

            return NormalizeCopy(result);
        }

        uint acc = 0;

        for (int i = 0; i < length; i++)
        {
            uint current = digits[i];
            result[i + wordShift] = (current << bitShift) | acc;
            acc = current >> (bitsPerWord - bitShift);
        }

        result[length + wordShift] = acc;
        return NormalizeCopy(result);
    }
    
    private static uint[] ShiftRightMagnitude(ReadOnlySpan<uint> digits, int shift)
    {
        int length = GetRealLength(digits);

        if (length == 0 || shift == 0)
        {
            return NormalizeCopy(digits);
        }

        int wordShift = shift / bitsPerWord;
        int bitShift = shift % bitsPerWord;

        if (wordShift >= length)
        {
            return [0u];
        }

        int resultLength = length - wordShift;
        uint[] result = new uint[resultLength];
        
        if (bitShift == 0)
        {
            for (int source = wordShift; source < length; source++)
            {
                result[source - wordShift] = digits[source];
            }

            return NormalizeCopy(result);
        }

        uint acc = 0;
        uint lowMask = (1u << bitShift) - 1u;
        
        for (int source = length - 1; source >= wordShift; source--)
        {
            uint current = digits[source];
            int destination = source - wordShift;
            result[destination] = (current >> bitShift) | (acc << (bitsPerWord - bitShift));
            acc = current & lowMask;
        }

        return NormalizeCopy(result);
    }
    
    private static int GetBitLength(ReadOnlySpan<uint> digits)
    {
        int length = GetRealLength(digits);

        if (length == 0)
        {
            return 0;
        }

        uint highestWord = digits[length - 1];
        return ((length - 1) * bitsPerWord) + (bitsPerWord - BitOperations.LeadingZeroCount(highestWord));
    }
    
    private static bool GetBit(ReadOnlySpan<uint> digits, int bitIndex)
    {
        int wordIndex = bitIndex / bitsPerWord;

        if (wordIndex >= digits.Length)
        {
            return false;
        }
        
        int offset = bitIndex % bitsPerWord;
        return ((digits[wordIndex] >> offset) & 1u) == 1u;
    }
    
    private static uint[] DivMagnitudes(ReadOnlySpan<uint> dividend, ReadOnlySpan<uint> divisor, out uint[] remainder)
    {
        uint[] normalizedDividend = NormalizeCopy(dividend);
        uint[] normalizedDivisor = NormalizeCopy(divisor);

        if (IsMagnitudeZero(normalizedDivisor))
        {
            throw new DivideByZeroException("Деление на ноль запрещено.");
        }

        if (CompareMagnitudes(normalizedDividend, normalizedDivisor) < 0)
        {
            remainder = normalizedDividend;
            return [0u];
        }

        int bitLength = GetBitLength(normalizedDividend);
        uint[] divRes = new uint[(bitLength + bitsPerWord - 1) / bitsPerWord];
        uint[] acc = [0u];

        for (int bit = bitLength - 1; bit >= 0; bit--)
        {
            acc = ShiftLeftMagnitude(acc, 1);

            if (GetBit(normalizedDividend, bit))
            {
                acc = AddUInt(acc, 1u);
            }
            
            if (CompareMagnitudes(acc, normalizedDivisor) >= 0)
            {
                acc = SubtractMagnitudes(acc, normalizedDivisor);
                divRes[bit / bitsPerWord] |= 1u << (bit % bitsPerWord);
            }
        }

        remainder = NormalizeCopy(acc);
        return NormalizeCopy(divRes);
    }
    
    private static uint DivSmall(uint[] digits, ref int length, uint divisor)
    {
        if (divisor == 0u)
        {
            throw new DivideByZeroException("Деление на ноль запрещено.");
        }

        uint remainder = 0;

        for (int i = length - 1; i >= 0; i--)
        {
            uint divResWord = 0;
            uint currentWord = digits[i];

            for (int bit = bitsPerWord - 1; bit >= 0; bit--)
            {
                remainder <<= 1;

                if (((currentWord >> bit) & 1u) != 0u)
                {
                    remainder |= 1u;
                }

                if (remainder >= divisor)
                {
                    remainder -= divisor;
                    divResWord |= 1u << bit;
                }
            }

            digits[i] = divResWord;
        }

        while (length > 0 && digits[length - 1] == 0u)
        {
            length--;
        }

        return remainder;
    }
    
    private static BetterBigInteger CompleteBinaryOperation(BetterBigInteger a, BetterBigInteger b, Func<uint, uint, uint> operation)
    {
        if (a is null)
        {
            throw new ArgumentNullException("Левый операнд не может быть null.");
        }

        if (b is null)
        {
            throw new ArgumentNullException("Правый операнд не может быть null.");
        }

        if (operation is null)
        {
            throw new ArgumentNullException("Побитовая операция не может быть null.");
        }

        int targetWordCount = Math.Max(a.WordCount, b.WordCount) + 1;
        uint[] leftWords = ToBinaryView(a, targetWordCount);
        uint[] rightWords = ToBinaryView(b, targetWordCount);
        uint[] resultWords = new uint[targetWordCount];

        for (int i = 0; i < targetWordCount; i++)
        {
            resultWords[i] = operation(leftWords[i], rightWords[i]);
        }

        return FromBinaryView(resultWords);
    }
    
    private static uint[] ToBinaryView(BetterBigInteger value, int wordCount)
    {
        uint[] words = new uint[wordCount];
        ReadOnlySpan<uint> digits = value.GetDigits();
        int copyLength = Math.Min(GetRealLength(digits), wordCount);

        for (int i = 0; i < copyLength; i++)
        {
            words[i] = digits[i];
        }

        if (!value.IsNegative)
        {
            return words;
        }

        for (int i = 0; i < words.Length; i++)
        {
            words[i] = ~words[i];
        }

        uint acc = 1;

        for (int i = 0; i < words.Length; i++)
        {
            uint sum = words[i] + acc;
            words[i] = sum;
            acc = sum == 0u ? 1u : 0u;

            if (acc == 0)
            {
                break;
            }
        }

        return words;
    }
    
    private static BetterBigInteger FromBinaryView(uint[] words)
    {
        bool isNegative = (words[^1] & 0x80000000u) != 0u;

        if (!isNegative)
        {
            return FromMagnitude(words, false);
        }
        
        uint[] magnitude = new uint[words.Length];
        
        for (int i = 0; i < words.Length; i++)
        {
            magnitude[i] = ~words[i];
        }
        
        uint acc = 1;
        
        for (int i = 0; i < magnitude.Length; i++)
        {
            uint sum = magnitude[i] + acc;
            magnitude[i] = sum;
            acc = sum == 0u ? 1u : 0u;

            if (acc == 0)
            {
                break;
            }
        }
        
        magnitude = NormalizeCopy(magnitude);
        return IsMagnitudeZero(magnitude) ? Zero() : FromMagnitude(magnitude, true);
    }
}

#endregion