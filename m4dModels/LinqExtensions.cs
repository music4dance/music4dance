namespace m4dModels;

public static class CollectionExtensions
{
    public static void AddRange<T>(this IList<T> toList, IList<T> fromList)
    {
        if (fromList == null)
        {
            return;
        }

        foreach (var t in fromList)
        {
            toList.Add(t);
        }
    }
}

public static class LinqExtensions
{
    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items,
        Func<T, TKey> property)
    {
        var comparer = new GeneralPropertyComparer<T, TKey>(property);
        return items.Distinct(comparer);
    }
}

public class GeneralPropertyComparer<T, TKey>(Func<T, TKey> expr) : IEqualityComparer<T>
{
    private Func<T, TKey> Expr { get; } = expr;

    public bool Equals(T left, T right)
    {
        var leftProp = Expr.Invoke(left);
        var rightProp = Expr.Invoke(right);
        return leftProp == null && rightProp == null || (!((leftProp == null) ^ (rightProp == null)) && leftProp.Equals(rightProp));
    }

    public int GetHashCode(T obj)
    {
        var prop = Expr.Invoke(obj);
        return prop == null ? 0 : prop.GetHashCode();
    }
}
