using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            var comparer = new GeneralPropertyComparer<T, TKey>(property);
            return items.Distinct(comparer);
        }
    }
    public class GeneralPropertyComparer<T, TKey> : IEqualityComparer<T>
    {
        private Func<T, TKey> Expr { get; }
        public GeneralPropertyComparer(Func<T, TKey> expr)
        {
            Expr = expr;
        }
        public bool Equals(T left, T right)
        {
            var leftProp = Expr.Invoke(left);
            var rightProp = Expr.Invoke(right);
            if (leftProp == null && rightProp == null)
                return true;
            if (leftProp == null ^ rightProp == null)
                return false;

            return leftProp.Equals(rightProp);
        }
        public int GetHashCode(T obj)
        {
            var prop = Expr.Invoke(obj);
            return (prop == null) ? 0 : prop.GetHashCode();
        }
    }
}
