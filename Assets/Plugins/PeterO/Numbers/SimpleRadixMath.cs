/*
Written by Peter O.
Any copyright to this work is released to the Public Domain.
In case this is not possible, this work is also
licensed under the Unlicense: https://unlicense.org/

 */
using System;

namespace PeterO.Numbers {
  // <summary>Implements the simplified arithmetic in Appendix A of the
  // General Decimal Arithmetic Specification. Unfortunately, it doesn't
  // pass all the test cases, since some aspects of the specification are left
  // open. For example: in which cases is the Clamped flag set? The test
  // cases set the Clamped flag in only a handful of test cases, all
  // within the <c>exp</c> operation.</summary>
  // <typeparam name='T'>Data type for a numeric value in a particular
  // radix.</typeparam>
  internal sealed class SimpleRadixMath<T> : IRadixMath<T> {
    private readonly IRadixMath<T> wrapper;

    public SimpleRadixMath(IRadixMath<T> wrapper) {
      this.wrapper = wrapper;
    }

    private static EContext GetContextWithFlags(EContext ctx) {
      return (ctx == null) ? ctx : ctx.WithBlankFlags();
    }

    private T SignalInvalid(EContext ctx) {
      if (this.GetHelper().GetArithmeticSupport() ==
        BigNumberFlags.FiniteOnly) {
        throw new ArithmeticException("Invalid operation");
      }
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |= EContext.FlagInvalid;
      }
      return this.GetHelper().CreateNewWithFlags(
        EInteger.Zero,
        EInteger.Zero,
        BigNumberFlags.FlagQuietNaN);
    }

    private T PostProcess(
      T thisValue,
      EContext ctxDest,
      EContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, false, false);
    }

    private T PostProcessAfterDivision(
      T thisValue,
      EContext ctxDest,
      EContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, true, false);
    }

    private T PostProcessAfterQuantize(
      T thisValue,
      EContext ctxDest,
      EContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, false, true);
    }

    private T PostProcessEx(
      T thisValue,
      EContext ctxDest,
      EContext ctxSrc,
      bool afterDivision,
      bool afterQuantize) {
      int thisFlags = this.GetHelper().GetFlags(thisValue);
      if (ctxDest != null && ctxSrc != null) {
        if (ctxDest.HasFlags) {
          if (!ctxSrc.ClampNormalExponents) {
            ctxSrc.Flags &= ~EContext.FlagClamped;
          }
          ctxDest.Flags |= ctxSrc.Flags;
          if ((ctxSrc.Flags & EContext.FlagSubnormal) != 0) {
            // Treat subnormal numbers as underflows
            ctxDest.Flags |= EContext.FlagUnderflow |
              EContext.FlagSubnormal | EContext.FlagInexact |
              EContext.FlagRounded;
          }
        }
      }
      if ((thisFlags & BigNumberFlags.FlagSpecial) != 0) {
        return (ctxDest.Flags == 0) ? this.SignalInvalid(ctxDest) : thisValue;
      }
      EInteger mant = this.GetHelper().GetMantissa(thisValue).Abs();
      if (mant.IsZero) {
        return afterQuantize ? this.GetHelper().CreateNewWithFlags(
          mant,
          this.GetHelper().GetExponent(thisValue),
          0) : this.wrapper.RoundToPrecision(
            this.GetHelper().ValueOf(0),
            ctxDest);
      }
      if (afterQuantize) {
        return thisValue;
      }
      EInteger exp = this.GetHelper().GetExponent(thisValue);
      if (exp.Sign > 0) {
        FastInteger fastExp = FastInteger.FromBig(exp);
        if (ctxDest == null || !ctxDest.HasMaxPrecision) {
          mant = this.GetHelper().MultiplyByRadixPower(mant, fastExp);
          return this.GetHelper().CreateNewWithFlags(
            mant,
            EInteger.Zero,
            thisFlags);
        }
        if (!ctxDest.ExponentWithinRange(exp)) {
          return thisValue;
        }
        FastInteger prec = FastInteger.FromBig(ctxDest.Precision);
        FastInteger digits = this.GetHelper().GetDigitLength(mant);
        prec.Subtract(digits);
        if (prec.Sign > 0 && prec.CompareTo(fastExp) >= 0) {
          mant = this.GetHelper().MultiplyByRadixPower(mant, fastExp);
          return this.GetHelper().CreateNewWithFlags(
            mant,
            EInteger.Zero,
            thisFlags);
        }
        if (afterDivision) {
          int radix = this.GetHelper().GetRadix();
          mant = NumberUtility.ReduceTrailingZeros(
              mant,
              fastExp,
              radix,
              null,
              null,
              null);
          thisValue = this.GetHelper().CreateNewWithFlags(
            mant,
            fastExp.ToEInteger(),
            thisFlags);
        }
      } else if (afterDivision && exp.Sign < 0) {
        FastInteger fastExp = FastInteger.FromBig(exp);
        int radix = this.GetHelper().GetRadix();
        mant = NumberUtility.ReduceTrailingZeros(
            mant, fastExp, radix, null, null, new FastInteger(0));
        thisValue = this.GetHelper().CreateNewWithFlags(
          mant,
          fastExp.ToEInteger(),
          thisFlags);
      }
      return thisValue;
    }

    private T ReturnQuietNaN(T thisValue, EContext ctx) {
      EInteger mant = this.GetHelper().GetMantissa(thisValue).Abs();
      var mantChanged = false;
      if (!mant.IsZero && ctx != null && ctx.HasMaxPrecision) {
        EInteger limit = this.GetHelper().MultiplyByRadixPower(
          EInteger.One,
          FastInteger.FromBig(ctx.Precision));
        if (mant.CompareTo(limit) >= 0) {
          mant %= (EInteger)limit;
          mantChanged = true;
        }
      }
      int flags = this.GetHelper().GetFlags(thisValue);
      if (!mantChanged && (flags & BigNumberFlags.FlagQuietNaN) != 0) {
        return thisValue;
      }
      flags &= BigNumberFlags.FlagNegative;
      flags |= BigNumberFlags.FlagQuietNaN;
      return this.GetHelper().CreateNewWithFlags(mant, EInteger.Zero, flags);
    }

    private T HandleNotANumber(T thisValue, T other, EContext ctx) {
      int thisFlags = this.GetHelper().GetFlags(thisValue);
      int otherFlags = this.GetHelper().GetFlags(other);
      // Check this value then the other value for signaling NaN
      if ((thisFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(thisValue, ctx);
      }
      if ((otherFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(other, ctx);
      }
      // Check this value then the other value for quiet NaN
      return ((thisFlags & BigNumberFlags.FlagQuietNaN) != 0) ?
        this.ReturnQuietNaN(thisValue, ctx) : (((otherFlags &
        BigNumberFlags.FlagQuietNaN) != 0) ? this.ReturnQuietNaN(
          other,
          ctx) : default(T));
    }

    private T CheckNotANumber3(
      T thisValue,
      T other,
      T other2,
      EContext ctx) {
      int thisFlags = this.GetHelper().GetFlags(thisValue);
      int otherFlags = this.GetHelper().GetFlags(other);
      int other2Flags = this.GetHelper().GetFlags(other2);
      // Check this value then the other value for signaling NaN
      if ((thisFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(thisValue, ctx);
      }
      if ((otherFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(other, ctx);
      }
      if ((other2Flags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(other, ctx);
      }
      // Check this value then the other value for quiet NaN
      return ((thisFlags & BigNumberFlags.FlagQuietNaN) != 0) ?
        this.ReturnQuietNaN(thisValue, ctx) : (((otherFlags &
        BigNumberFlags.FlagQuietNaN) != 0) ? this.ReturnQuietNaN(
          other,
          ctx) :
        (((other2Flags & BigNumberFlags.FlagQuietNaN) !=
        0) ? this.ReturnQuietNaN(other, ctx) : default(T)));
    }

    private T SignalingNaNInvalid(T value, EContext ctx) {
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |= EContext.FlagInvalid;
      }
      return this.ReturnQuietNaN(value, ctx);
    }

    private T CheckNotANumber1(T val, EContext ctx) {
      return this.HandleNotANumber(val, val, ctx);
    }

    private T CheckNotANumber2(T val, T val2, EContext ctx) {
      return this.HandleNotANumber(val, val2, ctx);
    }

    private T PreRound(T val, EContext ctx) {
      return NumberUtility.PreRound(val, ctx, this.wrapper);
    }

    public T DivideToIntegerNaturalScale(
      T thisValue,
      T divisor,
      EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.DivideToIntegerNaturalScale(
          thisValue,
          divisor,
          ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    public T DivideToIntegerZeroScale(
      T thisValue,
      T divisor,
      EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.DivideToIntegerZeroScale(
          thisValue,
          divisor,
          ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    public T Abs(T value, EContext ctx) {
      T ret = this.CheckNotANumber1(value, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      value = this.PreRound(value, ctx2);
      value = this.wrapper.Abs(value, ctx2);
      return this.PostProcess(value, ctx, ctx2);
    }

    public T Negate(T value, EContext ctx) {
      T ret = this.CheckNotANumber1(value, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      value = this.PreRound(value, ctx2);
      value = this.wrapper.Negate(value, ctx2);
      return this.PostProcess(value, ctx, ctx2);
    }

    public T Remainder(
      T thisValue,
      T divisor,
      EContext ctx,
      bool roundAfterDivide) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.Remainder(
          thisValue,
          divisor,
          ctx2,
          roundAfterDivide);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T RemainderNear(T thisValue, T divisor, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.RemainderNear(thisValue, divisor, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T Pi(EContext ctx) {
      return this.wrapper.Pi(ctx);
    }

    public T Power(T thisValue, T pow, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, pow, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      // Console.WriteLine("op was " + thisValue + ", "+pow);
      thisValue = this.PreRound(thisValue, ctx2);
      pow = this.PreRound(pow, ctx2);
      // Console.WriteLine("op now " + thisValue + ", "+pow);
      int powSign = this.GetHelper().GetSign(pow);
      thisValue = (powSign == 0 && this.GetHelper().GetSign(thisValue) == 0) ?
        this.wrapper.RoundToPrecision(this.GetHelper().ValueOf(1), ctx2) :
        this.wrapper.Power(thisValue, pow, ctx2);
      // Console.WriteLine("was " + thisValue);
      thisValue = this.PostProcessAfterDivision(thisValue, ctx, ctx2);
      // Console.WriteLine("now " + thisValue);
      return thisValue;
    }

    public T Ln(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.Ln(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public IRadixMathHelper<T> GetHelper() {
      return this.wrapper.GetHelper();
    }

    public T Exp(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.Exp(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T SquareRoot(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      // Console.WriteLine("op was " + thisValue);
      thisValue = this.PreRound(thisValue, ctx2);
      // Console.WriteLine("op now " + thisValue);
      thisValue = this.wrapper.SquareRoot(thisValue, ctx2);
      // Console.WriteLine("result was " + thisValue);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T NextMinus(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.NextMinus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T NextToward(T thisValue, T otherValue, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, otherValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      otherValue = this.PreRound(otherValue, ctx2);
      thisValue = this.wrapper.NextToward(thisValue, otherValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T NextPlus(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.NextPlus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T DivideToExponent(
      T thisValue,
      T divisor,
      EInteger desiredExponent,
      EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.DivideToExponent(
          thisValue,
          divisor,
          desiredExponent,
          ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    // <summary>Divides two T objects.</summary>
    // <param name='thisValue'></param>
    // <summary>Divides two T objects.</summary>
    // <param name='thisValue'></param>
    // <param name='divisor'></param>
    // <param name='ctx'> (3).</param>
    // <returns>The quotient of the two objects.</returns>
    public T Divide(T thisValue, T divisor, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      divisor = this.PreRound(divisor, ctx2);
      thisValue = this.wrapper.Divide(thisValue, divisor, ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    public T MinMagnitude(T a, T b, EContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      a = this.PreRound(a, ctx2);
      b = this.PreRound(b, ctx2);
      a = this.wrapper.MinMagnitude(a, b, ctx2);
      return this.PostProcess(a, ctx, ctx2);
    }

    public T MaxMagnitude(T a, T b, EContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      a = this.PreRound(a, ctx2);
      b = this.PreRound(b, ctx2);
      a = this.wrapper.MaxMagnitude(a, b, ctx2);
      return this.PostProcess(a, ctx, ctx2);
    }

    public T Max(T a, T b, EContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      a = this.PreRound(a, ctx2);
      b = this.PreRound(b, ctx2);
      // choose the left operand if both are equal
      a = (this.CompareTo(a, b) >= 0) ? a : b;
      return this.PostProcess(a, ctx, ctx2);
    }

    public T Min(T a, T b, EContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      a = this.PreRound(a, ctx2);
      b = this.PreRound(b, ctx2);
      // choose the left operand if both are equal
      a = (this.CompareTo(a, b) <= 0) ? a : b;
      return this.PostProcess(a, ctx, ctx2);
    }

    public T Multiply(T thisValue, T other, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, other, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      other = this.PreRound(other, ctx2);
      thisValue = this.wrapper.Multiply(thisValue, other, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T MultiplyAndAdd(
      T thisValue,
      T multiplicand,
      T augend,
      EContext ctx) {
      T ret = this.CheckNotANumber3(thisValue, multiplicand, augend, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      multiplicand = this.PreRound(multiplicand, ctx2);
      augend = this.PreRound(augend, ctx2);
      // the only time the first operand to the addition can be
      // 0 is if either thisValue rounded or multiplicand
      // rounded is 0
      bool zeroA = this.GetHelper().GetSign(thisValue) == 0 ||
        this.GetHelper().GetSign(multiplicand) == 0;
      bool zeroB = this.GetHelper().GetSign(augend) == 0;
      if (zeroA) {
        thisValue = zeroB ?
          this.wrapper.RoundToPrecision(this.GetHelper().ValueOf(0), ctx2) :
          augend;
        thisValue = this.RoundToPrecision(thisValue, ctx2);
      } else {
        thisValue = !zeroB ? this.wrapper.MultiplyAndAdd(
            thisValue,
            multiplicand,
            augend,
            ctx2) : this.wrapper.Multiply(thisValue, multiplicand, ctx2);
      }
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T Plus(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.Plus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T RoundToPrecision(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.RoundToPrecision(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T Quantize(T thisValue, T otherValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      // Console.WriteLine("was: "+thisValue+", "+otherValue);
      thisValue = this.PreRound(thisValue, ctx2);
      // Console.WriteLine("now: "+thisValue+", "+otherValue);
      otherValue = this.PreRound(otherValue, ctx2);
      // Apparently, subnormal values of "otherValue" raise
      // an invalid operation flag, according to the test cases
      EContext ctx3 = ctx2 == null ? null : ctx2.WithBlankFlags();
      this.wrapper.RoundToPrecision(otherValue, ctx3);
      if (ctx3 != null && (ctx3.Flags & EContext.FlagSubnormal) != 0) {
        return this.SignalInvalid(ctx);
      }
      thisValue = this.wrapper.Quantize(thisValue, otherValue, ctx2);
      // Console.WriteLine("result: "+thisValue);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T RoundToExponentExact(
      T thisValue,
      EInteger expOther,
      EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentExact(thisValue, expOther, ctx);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T RoundToExponentSimple(
      T thisValue,
      EInteger expOther,
      EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentSimple(
          thisValue,
          expOther,
          ctx2);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T RoundToExponentNoRoundedFlag(
      T thisValue,
      EInteger exponent,
      EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentNoRoundedFlag(
          thisValue,
          exponent,
          ctx);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T Reduce(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      thisValue = this.wrapper.Reduce(thisValue, ctx);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T Add(T thisValue, T other, EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, other, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.PreRound(thisValue, ctx2);
      other = this.PreRound(other, ctx2);
      bool zeroA = this.GetHelper().GetSign(thisValue) == 0;
      bool zeroB = this.GetHelper().GetSign(other) == 0;
      if (zeroA) {
        thisValue = zeroB ?
          this.wrapper.RoundToPrecision(this.GetHelper().ValueOf(0), ctx2) :
          other;
        thisValue = this.RoundToPrecision(thisValue, ctx2);
      } else {
        thisValue = (!zeroB) ? this.wrapper.AddEx(
          thisValue,
          other,
          ctx2,
          true) :
          this.RoundToPrecision(thisValue, ctx2);
      }
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    public T AddEx(
      T thisValue,
      T other,
      EContext ctx,
      bool roundToOperandPrecision) {
      // NOTE: Ignores roundToOperandPrecision
      return this.Add(thisValue, other, ctx);
    }

    public T CompareToWithContext(
      T thisValue,
      T otherValue,
      bool treatQuietNansAsSignaling,
      EContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, otherValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      thisValue = this.PreRound(thisValue, ctx);
      otherValue = this.PreRound(otherValue, ctx);
      return this.wrapper.CompareToWithContext(
          thisValue,
          otherValue,
          treatQuietNansAsSignaling,
          ctx);
    }

    public int CompareTo(T thisValue, T otherValue) {
      return this.wrapper.CompareTo(thisValue, otherValue);
    }

    public T SignalOverflow(EContext ctx, bool neg) {
      EContext ctx2 = GetContextWithFlags(ctx);
      T thisValue = this.wrapper.SignalOverflow(ctx, neg);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    public T RoundAfterConversion(T thisValue, EContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      if (this.GetHelper().GetSign(thisValue) == 0) {
        return this.wrapper.RoundToPrecision(this.GetHelper().ValueOf(0),
          ctx);
      }
      EContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.wrapper.RoundToPrecision(thisValue, ctx2);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }
  }
}
