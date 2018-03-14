﻿namespace NadekoBot.Core.Common
{
    public struct ShmartNumber
    {
        public long Value { get; }
        public string Input { get; }

        public ShmartNumber(long val, string input = null)
        {
            Value = val;
            Input = input;
        }

        public static implicit operator ShmartNumber(long num)
        {
            return new ShmartNumber(num);
        }
        public static implicit operator long(ShmartNumber num)
        {
            return num.Value;
        }

        public static implicit operator ShmartNumber(int num)
        {
            return new ShmartNumber(num);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
