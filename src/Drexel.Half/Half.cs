using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    /// <summary>
    /// An IEEE 754 compliant float16 type.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Half : IComparable, IComparable<Half>, IEquatable<Half>
    {
        // Constants duplicated from other classes which are not available in this version
        private const int DoubleSignShift = 63;
        private const int DoubleExponentShift = 52;
        private const ulong DoubleExponentMask = 0x7FF0_0000_0000_0000;
        private const ulong DoubleSignificandMask = 0x000F_FFFF_FFFF_FFFF;
        private const ulong DoubleSignMask = 0x8000_0000_0000_0000;
        private const int FloatSignShift = 31;
        private const int FloatExponentShift = 23;
        private const uint FloatExponentMask = 0x7F80_0000;
        private const uint FloatSignificandMask = 0x007F_FFFF;
        private const uint FloatSignMask = 0x8000_0000;

        private const NumberStyles DefaultParseStyle = NumberStyles.Float | NumberStyles.AllowThousands;

        // Constants for manipulating the private bit-representation
        private const ushort SignMask = 0x8000;
        private const ushort SignShift = 15;
        private const ushort ShiftedSignMask = SignMask >> SignShift;

        private const ushort ExponentMask = 0x7C00;
        private const ushort ExponentShift = 10;
        private const ushort ShiftedExponentMask = ExponentMask >> ExponentShift;

        private const ushort SignificandMask = 0x03FF;
        private const ushort SignificandShift = 0;

        private const ushort MinSign = 0;
        private const ushort MaxSign = 1;

        private const ushort MinExponent = 0x00;
        private const ushort MaxExponent = 0x1F;

        private const ushort MinSignificand = 0x0000;
        private const ushort MaxSignificand = 0x03FF;

        /// <value>
        /// 0.0
        /// </value>
        private const ushort PositiveZeroBits = 0x0000;

        /// <value>
        /// -0.0
        /// </value>
        private const ushort NegativeZeroBits = 0x8000;

        /// <value>
        /// 5.9604645E-08
        /// </value>
        private const ushort EpsilonBits = 0x0001;

        /// <value>
        /// 1.0 / 0.0
        /// </value>
        private const ushort PositiveInfinityBits = 0x7C00;

        /// <value>
        /// -1.0 / 0.0
        /// </value>
        private const ushort NegativeInfinityBits = 0xFC00;

        /// <value>
        ///  0.0 / 0.0
        /// </value>
        private const ushort NegativeQNaNBits = 0xFE00;

        /// <value>
        /// -65504
        /// </value>
        private const ushort MinValueBits = 0xFBFF;

        /// <value>
        /// 65504
        /// </value>
        private const ushort MaxValueBits = 0x7BFF;

        /// <summary>
        /// Represents the smallest positive <see cref="Half"/> value that is greater than zero.
        /// </summary>
        public static Half Epsilon => new Half(EpsilonBits);

        /// <summary>
        /// Gets a value representing positive infinity.
        /// </summary>
        public static Half PositiveInfinity => new Half(PositiveInfinityBits);

        /// <summary>
        /// Gets a value representing negative infinity.
        /// </summary>
        public static Half NegativeInfinity => new Half(NegativeInfinityBits);

        /// <summary>
        /// Gets a value representing not a number (NaN). This field is constant.
        /// </summary>
        public static Half NaN => new Half(NegativeQNaNBits);

        /// <summary>
        /// Gets a value representing the smallest possible value of <see cref="Half"/>.
        /// </summary>
        public static Half MinValue => new Half(MinValueBits);

        /// <summary>
        /// Gets a value representing the largest possible value of <see cref="Half"/>.
        /// </summary>
        public static Half MaxValue => new Half(MaxValueBits);

        // We use these explicit definitions to avoid the confusion between 0.0 and -0.0.
        private static readonly Half PositiveZero = new Half(PositiveZeroBits);
        private static readonly Half NegativeZero = new Half(NegativeZeroBits);

        private readonly ushort _value;

        internal Half(ushort value)
        {
            _value = value;
        }

        private Half(bool sign, ushort exp, ushort sig)
            => _value = (ushort)(((sign ? 1 : 0) << SignShift) + (exp << ExponentShift) + sig);

        private sbyte Exponent
        {
            get
            {
                return (sbyte)((_value & ExponentMask) >> ExponentShift);
            }
        }

        private ushort Significand
        {
            get
            {
                return (ushort)((_value & SignificandMask) >> SignificandShift);
            }
        }

        public static bool operator <(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is unordered with respect to everything, including itself.
                return false;
            }

            bool leftIsNegative = IsNegative(left);

            if (leftIsNegative != IsNegative(right))
            {
                // When the signs of left and right differ, we know that left is less than right if it is
                // the negative value. The exception to this is if both values are zero, in which case IEEE
                // says they should be equal, even if the signs differ.
                return leftIsNegative && !AreZero(left, right);
            }
            return (left._value < right._value) ^ leftIsNegative;
        }

        public static bool operator >(Half left, Half right)
        {
            return right < left;
        }

        public static bool operator <=(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is unordered with respect to everything, including itself.
                return false;
            }

            bool leftIsNegative = IsNegative(left);

            if (leftIsNegative != IsNegative(right))
            {
                // When the signs of left and right differ, we know that left is less than right if it is
                // the negative value. The exception to this is if both values are zero, in which case IEEE
                // says they should be equal, even if the signs differ.
                return leftIsNegative || AreZero(left, right);
            }
            return (left._value <= right._value) ^ leftIsNegative;
        }

        public static bool operator >=(Half left, Half right)
        {
            return right <= left;
        }

        public static bool operator ==(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is not equal to anything, including itself.
                return false;
            }

            // IEEE defines that positive and negative zero are equivalent.
            return (left._value == right._value) || AreZero(left, right);
        }

        public static bool operator !=(Half left, Half right)
        {
            return !(left == right);
        }

        /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
        public static bool IsFinite(Half value)
        {
            return StripSign(value) < PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is infinite.</summary>
        public static bool IsInfinity(Half value)
        {
            return StripSign(value) == PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is NaN.</summary>
        public static bool IsNaN(Half value)
        {
            return StripSign(value) > PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is negative.</summary>
        public static bool IsNegative(Half value)
        {
            return (short)(value._value) < 0;
        }

        /// <summary>Determines whether the specified value is negative infinity.</summary>
        public static bool IsNegativeInfinity(Half value)
        {
            return value._value == NegativeInfinityBits;
        }

        /// <summary>Determines whether the specified value is normal.</summary>
        // This is probably not worth inlining, it has branches and should be rarely called
        public static bool IsNormal(Half value)
        {
            uint absValue = StripSign(value);
            return (absValue < PositiveInfinityBits)    // is finite
                && (absValue != 0)                      // is not zero
                && ((absValue & ExponentMask) != 0);    // is not subnormal (has a non-zero exponent)
        }

        /// <summary>Determines whether the specified value is positive infinity.</summary>
        public static bool IsPositiveInfinity(Half value)
        {
            return value._value == PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is subnormal.</summary>
        // This is probably not worth inlining, it has branches and should be rarely called
        public static bool IsSubnormal(Half value)
        {
            uint absValue = StripSign(value);
            return (absValue < PositiveInfinityBits)    // is finite
                && (absValue != 0)                      // is not zero
                && ((absValue & ExponentMask) == 0);    // is subnormal (has a zero exponent)
        }

        public static Half Parse(string s) => (Half)float.Parse(s);

        public static Half Parse(string s, NumberStyles style) => (Half)float.Parse(s, style);

        public static Half Parse(string s, IFormatProvider? provider) => (Half)float.Parse(s, provider);

        public static Half Parse(string s, NumberStyles style = DefaultParseStyle, IFormatProvider? provider = null) =>
            (Half)float.Parse(s, style, provider);

        public static bool TryParse(string? s, out Half result)
        {
            if (float.TryParse(s, out float buffer))
            {
                try
                {
                    result = (Half)buffer;
                    return true;
                }
                catch
                {
                    result = default;
                    return false;
                }
            }

            result = default;
            return false;
        }

        public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Half result)
        {
            if (float.TryParse(s, style, provider, out float buffer))
            {
                try
                {
                    result = (Half)buffer;
                }
                catch
                {
                    result = default;
                    return false;
                }
            }

            result = default;
            return false;
        }

        private static bool AreZero(Half left, Half right)
        {
            // IEEE defines that positive and negative zero are equal, this gives us a quick equality check
            // for two values by or'ing the private bits together and stripping the sign. They are both zero,
            // and therefore equivalent, if the resulting value is still zero.
            return (ushort)((left._value | right._value) & ~SignMask) == 0;
        }

        private static bool IsNaNOrZero(Half value)
        {
            return ((value._value - 1) & ~SignMask) >= PositiveInfinityBits;
        }

        private static uint StripSign(Half value)
        {
            return (ushort)(value._value & ~SignMask);
        }

        /// <summary>
        /// Compares this object to another object, returning an integer that indicates the relationship.
        /// </summary>
        /// <returns>A value less than zero if this is less than <paramref name="obj"/>, zero if this is equal to <paramref name="obj"/>, or a value greater than zero if this is greater than <paramref name="obj"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not of type <see cref="Half"/>.</exception>
        public int CompareTo(object? obj)
        {
            if (!(obj is Half))
            {
                return (obj is null) ? 1 : throw new ArgumentException("SR.Arg_MustBeHalf");
            }
            return CompareTo((Half)(obj));
        }

        /// <summary>
        /// Compares this object to another object, returning an integer that indicates the relationship.
        /// </summary>
        /// <returns>A value less than zero if this is less than <paramref name="other"/>, zero if this is equal to <paramref name="other"/>, or a value greater than zero if this is greater than <paramref name="other"/>.</returns>
        public int CompareTo(Half other)
        {
            if (this < other)
            {
                return -1;
            }

            if (this > other)
            {
                return 1;
            }

            if (this == other)
            {
                return 0;
            }

            if (IsNaN(this))
            {
                return IsNaN(other) ? 0 : -1;
            }

            Debug.Assert(IsNaN(other));
            return 1;
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified <paramref name="obj"/>.
        /// </summary>
        public override bool Equals(object? obj) => (obj is Half other) && Equals(other);

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified <paramref name="other"/> value.
        /// </summary>
        public bool Equals(Half other)
        {
            return _value == other._value
                || AreZero(this, other)
                || (IsNaN(this) && IsNaN(other));
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            if (IsNaNOrZero(this))
            {
                // All NaNs should have the same hash code, as should both Zeros.
                return _value & PositiveInfinityBits;
            }

            return _value;
        }

        /// <summary>
        /// Returns a string representation of the current value.
        /// </summary>
        public override string ToString() => ((float)this).ToString();

        public string ToString(string? format) => ((float)this).ToString(format);

        public string ToString(IFormatProvider? provider) => ((float)this).ToString(provider);

        public string ToString(string? format, IFormatProvider? provider) => ((float)this).ToString(format, provider);

        // -----------------------Start of to-half conversions-------------------------

        public static explicit operator Half(float value)
        {
            const int SingleMaxExponent = 0xFF;

            uint floatInt = BitConverter.SingleToUInt32Bits(value);
            bool sign = (floatInt & FloatSignMask) >> FloatSignShift != 0;
            int exp = (int)(floatInt & FloatExponentMask) >> FloatExponentShift;
            uint sig = floatInt & FloatSignificandMask;

            if (exp == SingleMaxExponent)
            {
                if (sig != 0) // NaN
                {
                    return CreateHalfNaN(sign, (ulong)sig << 41); // Shift the significand bits to the left end
                }
                return sign ? NegativeInfinity : PositiveInfinity;
            }

            uint sigHalf = sig >> 9 | ((sig & 0x1FFU) != 0 ? 1U : 0U); // RightShiftJam

            if ((exp | (int)sigHalf) == 0)
            {
                return new Half(sign, 0, 0);
            }

            return new Half(RoundPackToHalf(sign, (short)(exp - 0x71), (ushort)(sigHalf | 0x4000)));
        }

        public static explicit operator Half(double value)
        {
            const int DoubleMaxExponent = 0x7FF;

            ulong doubleInt = BitConverter.DoubleToUInt64Bits(value);
            bool sign = (doubleInt & DoubleSignMask) >> DoubleSignShift != 0;
            int exp = (int)((doubleInt & DoubleExponentMask) >> DoubleExponentShift);
            ulong sig = doubleInt & DoubleSignificandMask;

            if (exp == DoubleMaxExponent)
            {
                if (sig != 0) // NaN
                {
                    return CreateHalfNaN(sign, sig << 12); // Shift the significand bits to the left end
                }
                return sign ? NegativeInfinity : PositiveInfinity;
            }

            uint sigHalf = (uint)ShiftRightJam(sig, 38);
            if ((exp | (int)sigHalf) == 0)
            {
                return new Half(sign, 0, 0);
            }
            return new Half(RoundPackToHalf(sign, (short)(exp - 0x3F1), (ushort)(sigHalf | 0x4000)));
        }

        // -----------------------Start of from-half conversions-------------------------
        public static explicit operator float(Half value)
        {
            bool sign = IsNegative(value);
            int exp = value.Exponent;
            uint sig = value.Significand;

            if (exp == MaxExponent)
            {
                if (sig != 0)
                {
                    return CreateSingleNaN(sign, (ulong)sig << 54);
                }
                return sign ? float.NegativeInfinity : float.PositiveInfinity;
            }

            if (exp == 0)
            {
                if (sig == 0)
                {
                    return BitConverter.UInt32BitsToSingle(sign ? FloatSignMask : 0); // Positive / Negative zero
                }

                Tuple<int, uint> buffer = NormSubnormalF16Sig(sig);
                exp = buffer.Item1 - 1;
                sig = buffer.Item2;
            }

            return CreateSingle(sign, (byte)(exp + 0x70), sig << 13);
        }

        public static explicit operator double(Half value)
        {
            bool sign = IsNegative(value);
            int exp = value.Exponent;
            uint sig = value.Significand;

            if (exp == MaxExponent)
            {
                if (sig != 0)
                {
                    return CreateDoubleNaN(sign, (ulong)sig << 54);
                }

                return sign ? double.NegativeInfinity : double.PositiveInfinity;
            }

            if (exp == 0)
            {
                if (sig == 0)
                {
                    return BitConverter.UInt64BitsToDouble(sign ? DoubleSignMask : 0); // Positive / Negative zero
                }

                Tuple<int, uint> buffer = NormSubnormalF16Sig(sig);
                exp = buffer.Item1 - 1;
                sig = buffer.Item2;
            }

            return CreateDouble(sign, (ushort)(exp + 0x3F0), (ulong)sig << 42);
        }

        // IEEE 754 specifies NaNs to be propagated
        internal static Half Negate(Half value)
        {
            return IsNaN(value) ? value : new Half((ushort)(value._value ^ SignMask));
        }

        /// <returns>(int Exp, uint Sig)</returns>
        private static Tuple<int, uint> NormSubnormalF16Sig(uint sig)
        {
            int shiftDist = BitOperations.LeadingZeroCount(sig) - 16 - 5;
            return new Tuple<int, uint>(1 - shiftDist, sig << shiftDist);
        }

        #region Utilities

        // Significand bits should be shifted towards to the left end before calling these methods
        // Creates Quiet NaN if significand == 0
        private static Half CreateHalfNaN(bool sign, ulong significand)
        {
            const uint NaNBits = ExponentMask | 0x200; // Most significant significand bit

            uint signInt = (sign ? 1U : 0U) << SignShift;
            uint sigInt = (uint)(significand >> 54);

            return BitConverter.UInt16BitsToHalf((ushort)(signInt | NaNBits | sigInt));
        }

        private static ushort RoundPackToHalf(bool sign, short exp, ushort sig)
        {
            const int RoundIncrement = 0x8; // Depends on rounding mode but it's always towards closest / ties to even
            int roundBits = sig & 0xF;

            if ((uint)exp >= 0x1D)
            {
                if (exp < 0)
                {
                    sig = (ushort)ShiftRightJam(sig, -exp);
                    exp = 0;
                    roundBits = sig & 0xF;
                }
                else if (exp > 0x1D || sig + RoundIncrement >= 0x8000) // Overflow
                {
                    return sign ? NegativeInfinityBits : PositiveInfinityBits;
                }
            }

            sig = (ushort)((sig + RoundIncrement) >> 4);
            sig &= (ushort)~(((roundBits ^ 8) != 0 ? 0 : 1) & 1);

            if (sig == 0)
            {
                exp = 0;
            }

            return new Half(sign, (ushort)exp, sig)._value;
        }

        // If any bits are lost by shifting, "jam" them into the LSB.
        // if dist > bit count, Will be 1 or 0 depending on i
        // (unlike bitwise operators that masks the lower 5 bits)
        private static uint ShiftRightJam(uint i, int dist)
            => dist < 31 ? (i >> dist) | (i << (-dist & 31) != 0 ? 1U : 0U) : (i != 0 ? 1U : 0U);

        private static ulong ShiftRightJam(ulong l, int dist)
            => dist < 63 ? (l >> dist) | (l << (-dist & 63) != 0 ? 1UL : 0UL) : (l != 0 ? 1UL : 0UL);

        private static float CreateSingleNaN(bool sign, ulong significand)
        {
            const uint NaNBits = FloatExponentMask | 0x400000; // Most significant significand bit

            uint signInt = (sign ? 1U : 0U) << FloatSignShift;
            uint sigInt = (uint)(significand >> 41);

            return BitConverter.UInt32BitsToSingle(signInt | NaNBits | sigInt);
        }

        private static double CreateDoubleNaN(bool sign, ulong significand)
        {
            const ulong NaNBits = DoubleExponentMask | 0x80000_00000000; // Most significant significand bit

            ulong signInt = (sign ? 1UL : 0UL) << DoubleSignShift;
            ulong sigInt = significand >> 12;

            return BitConverter.UInt64BitsToDouble(signInt | NaNBits | sigInt);
        }

        private static float CreateSingle(bool sign, byte exp, uint sig)
            => BitConverter.UInt32BitsToSingle(((sign ? 1U : 0U) << FloatSignShift) + ((uint)exp << FloatExponentShift) + sig);

        private static double CreateDouble(bool sign, ushort exp, ulong sig)
            => BitConverter.UInt64BitsToDouble(((sign ? 1UL : 0UL) << DoubleSignShift) + ((ulong)exp << DoubleExponentShift) + sig);

        #endregion

        private static class BitOperations
        {
            private static int[] Log2DeBruijn =>
                new int[32]
                {
                    00, 09, 01, 10, 13, 21, 02, 29,
                    11, 14, 16, 18, 22, 25, 03, 30,
                    08, 12, 20, 28, 15, 17, 24, 07,
                    19, 27, 23, 06, 26, 05, 04, 31
                };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int LeadingZeroCount(uint value)
            {
                // Unguarded fallback contract is 0->31, BSR contract is 0->undefined
                if (value == 0)
                {
                    return 32;
                }

                return 31 ^ Log2SoftwareFallback(value);
            }

            /// <summary>
            /// Returns the integer (floor) log of the specified value, base 2.
            /// Note that by convention, input value 0 returns 0 since Log(0) is undefined.
            /// Does not directly use any hardware intrinsics, nor does it incur branching.
            /// </summary>
            /// <param name="value">The value.</param>
            private static unsafe int Log2SoftwareFallback(uint value)
            {
                // No AggressiveInlining due to large method size
                // Has conventional contract 0->0 (Log(0) is undefined)

                // Fill trailing zeros with ones, eg 00010010 becomes 00011111
                value |= value >> 01;
                value |= value >> 02;
                value |= value >> 04;
                value |= value >> 08;
                value |= value >> 16;

                fixed (int* p = Log2DeBruijn)
                {
                    // NOTE: I have probably mangled this while getting it to compile, because I'm not entirely sure
                    // how `ref MemoryMarshal.GetReference(Log2DeBruigin)` works when the return type of this method is
                    // `int`, but the underlying array type is `byte`.

                    // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
                    return Unsafe.AddByteOffset<int>(
                        // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
                        ref Log2DeBruijn[0],
                        // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                        (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
                }
            }
        }

        private static class BitConverter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe int SingleToInt32Bits(float value) => *((int*)&value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe uint SingleToUInt32Bits(float value) => (uint)SingleToInt32Bits(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe long DoubleToInt64Bits(double value) => *((long*)&value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe ulong DoubleToUInt64Bits(double value) => (ulong)DoubleToInt64Bits(value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe double Int64BitsToDouble(long value) => *((double*)&value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe double UInt64BitsToDouble(ulong value) => Int64BitsToDouble((long)value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe float Int32BitsToSingle(int value) => *((float*)&value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe float UInt32BitsToSingle(uint value) => Int32BitsToSingle((int)value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe Half Int16BitsToHalf(short value) => *(Half*)&value;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe Half UInt16BitsToHalf(ushort value) => Int16BitsToHalf((short)value);
        }
    }
}