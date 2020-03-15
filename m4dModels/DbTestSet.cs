// CORETODO: Figure out testing

//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Threading;
//using System.Threading.Tasks;

//namespace m4dModels
//{
//    public class TestDbSet<TEntity> : DbSet<TEntity>, IQueryable, IEnumerable<TEntity>, IDbAsyncEnumerable<TEntity>
//        where TEntity : class
//    {
//        readonly ObservableCollection<TEntity> _data;
//        readonly IQueryable _query;

//        public TestDbSet()
//        {
//            _data = new ObservableCollection<TEntity>();
//            _query = _data.AsQueryable();
//        }

//        public override TEntity Add(TEntity item)
//        {
//            _data.Add(item);
//            return item;
//        }

//        public override TEntity Remove(TEntity item)
//        {
//            _data.Remove(item);
//            return item;
//        }

//        public override IEnumerable<TEntity> RemoveRange(IEnumerable<TEntity> entities)
//        {
//            var removeRange = entities as IList<TEntity> ?? entities.ToList();
//            foreach (var e in removeRange)
//            {
//                _data.Remove(e);
//            }
//            return removeRange;
//        }

//        public override TEntity Attach(TEntity item)
//        {
//            _data.Add(item);
//            return item;
//        }

//        public override TEntity Create()
//        {
//            return Activator.CreateInstance<TEntity>();
//        }

//        public override TDerivedEntity Create<TDerivedEntity>()
//        {
//            return Activator.CreateInstance<TDerivedEntity>();
//        }

//        public override ObservableCollection<TEntity> Local => _data;

//        Type IQueryable.ElementType => _query.ElementType;

//        Expression IQueryable.Expression => _query.Expression;

//        IQueryProvider IQueryable.Provider => new TestDbAsyncQueryProvider<TEntity>(_query.Provider);

//        IEnumerator IEnumerable.GetEnumerator()
//        {
//            return _data.GetEnumerator();
//        }

//        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator()
//        {
//            return _data.GetEnumerator();
//        }

//        IDbAsyncEnumerator<TEntity> IDbAsyncEnumerable<TEntity>.GetAsyncEnumerator()
//        {
//            return new TestDbAsyncEnumerator<TEntity>(_data.GetEnumerator());
//        }
//    }

//    internal class TestDbAsyncQueryProvider<TEntity> : IDbAsyncQueryProvider
//    {
//        private readonly IQueryProvider _inner;

//        internal TestDbAsyncQueryProvider(IQueryProvider inner)
//        {
//            _inner = inner;
//        }

//        public IQueryable CreateQuery(Expression expression)
//        {
//            return new TestDbAsyncEnumerable<TEntity>(expression);
//        }

//        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
//        {
//            return new TestDbAsyncEnumerable<TElement>(expression);
//        }

//        public object Execute(Expression expression)
//        {
//            return _inner.Execute(expression);
//        }

//        public TResult Execute<TResult>(Expression expression)
//        {
//            return _inner.Execute<TResult>(expression);
//        }

//        public Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
//        {
//            return Task.FromResult(Execute(expression));
//        }

//        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
//        {
//            return Task.FromResult(Execute<TResult>(expression));
//        }
//    }

//    internal class TestDbAsyncEnumerable<T> : EnumerableQuery<T>, IDbAsyncEnumerable<T>, IQueryable<T>
//    {
//        public TestDbAsyncEnumerable(IEnumerable<T> enumerable)
//            : base(enumerable)
//        { }

//        public TestDbAsyncEnumerable(Expression expression)
//            : base(expression)
//        { }

//        public IDbAsyncEnumerator<T> GetAsyncEnumerator()
//        {
//            return new TestDbAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
//        }

//        IDbAsyncEnumerator IDbAsyncEnumerable.GetAsyncEnumerator()
//        {
//            return GetAsyncEnumerator();
//        }

//        IQueryProvider IQueryable.Provider => new TestDbAsyncQueryProvider<T>(this);
//    }

//    internal class TestDbAsyncEnumerator<T> : IDbAsyncEnumerator<T>
//    {
//        private readonly IEnumerator<T> _inner;

//        public TestDbAsyncEnumerator(IEnumerator<T> inner)
//        {
//            _inner = inner;
//        }

//        public void Dispose()
//        {
//            _inner.Dispose();
//        }

//        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
//        {
//            return Task.FromResult(_inner.MoveNext());
//        }

//        public T Current => _inner.Current;

//        object IDbAsyncEnumerator.Current => Current;
//    }

//    public class TagGroupSet : TestDbSet<TagGroup>
//    {
//        public override TagGroup Find(params object[] keyValues)
//        {
//            var id = keyValues.Single() as string;
//            return id == null ? null : this.SingleOrDefault(tt => string.Equals(tt.Key,id,StringComparison.OrdinalIgnoreCase));
//        }
//    }

//    public class SearchSet : TestDbSet<Search>
//    {
//        public override Search Find(params object[] keyValues)
//        {
//            var id = keyValues.Single();
//            if (!(id is long))
//                return null;
//            return this.SingleOrDefault(s => s.Id == (long)id);
//        }
//    }

//}
