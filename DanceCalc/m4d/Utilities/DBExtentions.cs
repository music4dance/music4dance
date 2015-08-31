using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace m4d.Utilities
{
    public static class DBExtentions
    {
        public static void UpdateManyToMany<TSingle, TMany>(
            this DbContext ctx,
            TSingle localItem,
            Func<TSingle, ICollection<TMany>> collectionSelector)
            where TSingle : class
            where TMany : class
        {
            var localItemDbSet = ctx.Set(typeof(TSingle)).Cast<TSingle>();
            var manyItemDbSet = ctx.Set(typeof(TMany)).Cast<TMany>();

            var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
            var tempSet = objectContext.CreateObjectSet<TSingle>();
            var localItemKeyNames = tempSet.EntitySet.ElementType.KeyMembers.Select(k => k.Name);

            var localItemKeysArray = localItemKeyNames.Select(kn => typeof(TSingle).GetProperty(kn).GetValue(localItem, null));

            localItemDbSet.Load();

            var dbVerOfLocalItem = localItemDbSet.Find(localItemKeysArray.ToArray());
            var localCol = collectionSelector(localItem) ?? Enumerable.Empty<TMany>();
            var dbColl = collectionSelector(dbVerOfLocalItem);
            dbColl.Clear();

            var tempSet1 = objectContext.CreateObjectSet<TMany>();
            var collectionKeyNames = tempSet1.EntitySet.ElementType.KeyMembers.Select(k => k.Name);

            var selectedDbCats = localCol
                .Select(c => collectionKeyNames.Select(kn => typeof(TMany).GetProperty(kn).GetValue(c, null)).ToArray())
                .Select(manyItemDbSet.Find);
            foreach (var xx in selectedDbCats)
            {
                dbColl.Add(xx);
            }
            ctx.Entry(dbVerOfLocalItem).CurrentValues.SetValues(localItem);
        }
    }
}