using System;
using System.Collections.Generic;

public class CustomComparer<T> : IComparer<T>
{
    private Comparison<T> _comparison;

    public CustomComparer(Comparison<T> comparison)
    {
        _comparison = comparison;
    }

    public int Compare(T x, T y)
    {
        return _comparison(x, y);
    }
}