using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace slskd.Tests.Unit.Common;

public class CommonExtensionsTests
{
    private class Simple
    {
        public int IntProp { get; set; }
        public string StringProp { get; set; }
        public bool BoolProp { get; set; }
        public decimal DecimalProp { get; set; }
    }

    private class WithNullable
    {
        public int? NullableInt { get; set; }
        public string StringProp { get; set; }
    }

    private class WithEnum
    {
        public DayOfWeek EnumProp { get; set; }
    }

    private class WithArray
    {
        public int[] ArrayProp { get; set; }
    }

    private class WithDictionary
    {
        public Dictionary<string, int> DictProp { get; set; }
    }

    private class Nested
    {
        public string Name { get; set; }
        public Simple Inner { get; set; }
    }

    public class DiffWith_NullHandling
    {
        [Fact]
        public void Both_Null_Returns_Empty()
        {
            object left = null;
            object right = null;

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Left_Null_Right_NonNull_Returns_Difference()
        {
            object left = null;
            object right = new Simple();

            var result = left.DiffWith(right);

            var (_, _, leftVal, rightVal) = Assert.Single(result);
            Assert.Null(leftVal);
            Assert.NotNull(rightVal);
        }

        [Fact]
        public void Left_NonNull_Right_Null_Returns_Difference()
        {
            object left = new Simple();
            object right = null;

            var result = left.DiffWith(right);

            var (_, _, leftVal, rightVal) = Assert.Single(result);
            Assert.NotNull(leftVal);
            Assert.Null(rightVal);
        }

        [Fact]
        public void Left_Null_Right_NonNull_FQN_Is_ParentFqn()
        {
            object left = null;
            object right = new Simple();

            var result = left.DiffWith(right, "root");

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("root", fqn);
        }

        [Fact]
        public void Left_Null_Right_NonNull_FQN_Is_Empty_When_No_ParentFqn()
        {
            object left = null;
            object right = new Simple();

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal(string.Empty, fqn);
        }

        [Fact]
        public void Right_Null_Left_NonNull_FQN_Is_Empty_When_No_ParentFqn()
        {
            object left = new Simple();
            object right = null;

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal(string.Empty, fqn);
        }

        [Fact]
        public void Mismatched_Types_Throws_InvalidCastException()
        {
            object left = new Simple();
            object right = new WithNullable();

            Assert.Throws<InvalidCastException>(() => left.DiffWith(right));
        }

        [Fact]
        public void Left_Null_Right_NonNull_Any_Type_Returns_Difference()
        {
            object left = null;
            object right = 42;

            var result = left.DiffWith(right);

            var (_, _, leftVal, rightVal) = Assert.Single(result);
            Assert.Null(leftVal);
            Assert.Equal(42, rightVal);
        }

        [Fact]
        public void Right_Null_Left_NonNull_Any_Type_Returns_Difference()
        {
            object left = 42;
            object right = null;

            var result = left.DiffWith(right);

            var (_, _, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal(42, leftVal);
            Assert.Null(rightVal);
        }
    }

    public class DiffWith_PrimitiveProperties
    {
        [Fact]
        public void Identical_Objects_Returns_Empty()
        {
            var left = new Simple { IntProp = 1, StringProp = "a", BoolProp = true, DecimalProp = 1.5m };
            var right = new Simple { IntProp = 1, StringProp = "a", BoolProp = true, DecimalProp = 1.5m };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Different_Int_Returns_Difference()
        {
            var left = new Simple { IntProp = 1 };
            var right = new Simple { IntProp = 2 };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("IntProp", fqn);
            Assert.Equal(1, leftVal);
            Assert.Equal(2, rightVal);
        }

        [Fact]
        public void Different_String_Returns_Difference()
        {
            var left = new Simple { StringProp = "foo" };
            var right = new Simple { StringProp = "bar" };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("StringProp", fqn);
        }

        [Fact]
        public void Different_Bool_Returns_Difference()
        {
            var left = new Simple { BoolProp = true };
            var right = new Simple { BoolProp = false };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("BoolProp", fqn);
        }

        [Fact]
        public void Different_Decimal_Returns_Difference()
        {
            var left = new Simple { DecimalProp = 1.1m };
            var right = new Simple { DecimalProp = 2.2m };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("DecimalProp", fqn);
        }

        [Fact]
        public void Multiple_Differences_All_Returned()
        {
            var left = new Simple { IntProp = 1, StringProp = "a" };
            var right = new Simple { IntProp = 2, StringProp = "b" };

            var result = left.DiffWith(right);

            Assert.Equal(2, result.Count());
        }
    }

    public class DiffWith_NullableProperties
    {
        [Fact]
        public void Both_Nullable_Null_No_Difference()
        {
            var left = new WithNullable { NullableInt = null };
            var right = new WithNullable { NullableInt = null };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Left_Nullable_Null_Right_HasValue_Returns_Difference()
        {
            var left = new WithNullable { NullableInt = null };
            var right = new WithNullable { NullableInt = 5 };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("NullableInt", fqn);
            Assert.Null(leftVal);
            Assert.Equal(5, rightVal);
        }

        [Fact]
        public void Right_Nullable_Null_Left_HasValue_Returns_Difference()
        {
            var left = new WithNullable { NullableInt = 5 };
            var right = new WithNullable { NullableInt = null };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("NullableInt", fqn);
            Assert.Equal(5, leftVal);
            Assert.Null(rightVal);
        }

        [Fact]
        public void Both_Nullable_Same_Value_No_Difference()
        {
            var left = new WithNullable { NullableInt = 7 };
            var right = new WithNullable { NullableInt = 7 };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void String_Null_Both_Sides_No_Difference()
        {
            var left = new WithNullable { StringProp = null };
            var right = new WithNullable { StringProp = null };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void String_Null_Left_NonNull_Right_Returns_Difference()
        {
            var left = new WithNullable { StringProp = null };
            var right = new WithNullable { StringProp = "hello" };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("StringProp", fqn);
            Assert.Null(leftVal);
            Assert.Equal("hello", rightVal);
        }
    }

    public class DiffWith_EnumProperties
    {
        [Fact]
        public void Same_Enum_No_Difference()
        {
            var left = new WithEnum { EnumProp = DayOfWeek.Monday };
            var right = new WithEnum { EnumProp = DayOfWeek.Monday };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Different_Enum_Returns_Difference()
        {
            var left = new WithEnum { EnumProp = DayOfWeek.Monday };
            var right = new WithEnum { EnumProp = DayOfWeek.Friday };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("EnumProp", fqn);
            Assert.Equal(DayOfWeek.Monday, leftVal);
            Assert.Equal(DayOfWeek.Friday, rightVal);
        }
    }

    public class DiffWith_ArrayProperties
    {
        [Fact]
        public void Same_Array_No_Difference()
        {
            var left = new WithArray { ArrayProp = [1, 2, 3] };
            var right = new WithArray { ArrayProp = [1, 2, 3] };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Different_Array_Returns_Difference()
        {
            var left = new WithArray { ArrayProp = [1, 2, 3] };
            var right = new WithArray { ArrayProp = [1, 2, 4] };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("ArrayProp", fqn);
        }

        [Fact]
        public void Different_Array_Length_Returns_Difference()
        {
            var left = new WithArray { ArrayProp = [1, 2] };
            var right = new WithArray { ArrayProp = [1, 2, 3] };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("ArrayProp", fqn);
        }

        [Fact]
        public void Both_Arrays_Null_No_Difference()
        {
            var left = new WithArray { ArrayProp = null };
            var right = new WithArray { ArrayProp = null };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Left_Array_Null_Right_NonNull_Returns_Difference()
        {
            var left = new WithArray { ArrayProp = null };
            var right = new WithArray { ArrayProp = [1] };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("ArrayProp", fqn);
            Assert.Null(leftVal);
            Assert.NotNull(rightVal);
        }
    }

    public class DiffWith_DictionaryProperties
    {
        [Fact]
        public void Same_Dictionary_No_Difference()
        {
            var left = new WithDictionary { DictProp = new Dictionary<string, int> { ["a"] = 1 } };
            var right = new WithDictionary { DictProp = new Dictionary<string, int> { ["a"] = 1 } };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Different_Dictionary_Returns_Difference()
        {
            var left = new WithDictionary { DictProp = new Dictionary<string, int> { ["a"] = 1 } };
            var right = new WithDictionary { DictProp = new Dictionary<string, int> { ["a"] = 2 } };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("DictProp", fqn);
        }

        [Fact]
        public void Both_Dictionaries_Null_No_Difference()
        {
            var left = new WithDictionary { DictProp = null };
            var right = new WithDictionary { DictProp = null };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Left_Dictionary_Null_Right_NonNull_Returns_Difference()
        {
            var left = new WithDictionary { DictProp = null };
            var right = new WithDictionary { DictProp = new Dictionary<string, int> { ["a"] = 1 } };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("DictProp", fqn);
        }
    }

    public class DiffWith_NestedObjects
    {
        [Fact]
        public void Identical_Nested_Objects_Returns_Empty()
        {
            var left = new Nested { Name = "x", Inner = new Simple { IntProp = 1 } };
            var right = new Nested { Name = "x", Inner = new Simple { IntProp = 1 } };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Different_Nested_Property_FQN_Is_Dotted()
        {
            var left = new Nested { Inner = new Simple { IntProp = 1 } };
            var right = new Nested { Inner = new Simple { IntProp = 9 } };

            var result = left.DiffWith(right);

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("Inner.IntProp", fqn);
        }

        [Fact]
        public void Both_Nested_Null_No_Difference()
        {
            var left = new Nested { Inner = null };
            var right = new Nested { Inner = null };

            var result = left.DiffWith(right);

            Assert.Empty(result);
        }

        [Fact]
        public void Left_Nested_Null_Right_NonNull_Returns_Difference()
        {
            var left = new Nested { Inner = null };
            var right = new Nested { Inner = new Simple() };

            var result = left.DiffWith(right);

            var (_, fqn, leftVal, rightVal) = Assert.Single(result);
            Assert.Equal("Inner", fqn);
            Assert.Null(leftVal);
            Assert.NotNull(rightVal);
        }

        [Fact]
        public void ParentFqn_Prepended_To_Nested_FQN()
        {
            var left = new Nested { Inner = new Simple { IntProp = 1 } };
            var right = new Nested { Inner = new Simple { IntProp = 2 } };

            var result = left.DiffWith(right, "root");

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("root.Inner.IntProp", fqn);
        }

        [Fact]
        public void Top_Level_Property_FQN_Includes_ParentFqn()
        {
            var left = new Simple { IntProp = 1 };
            var right = new Simple { IntProp = 2 };

            var result = left.DiffWith(right, "root");

            var (_, fqn, _, _) = Assert.Single(result);
            Assert.Equal("root.IntProp", fqn);
        }
    }
}
