﻿using BusterWood.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Linq
{
    public interface IAsyncEnumerable
    {
        IAsyncEnumerator GetAsyncEnumerator();
    }

    public interface IAsyncEnumerator : IDisposable
    {
        /// <summary>The current value of the sequence</summary>
        object Current { get; }

        /// <summary>Move to the next item in the sequence, returns TRUE if moved successfully, FALSE if there are no more items.</summary>
        Task<bool> MoveNextAsync(CancellationToken cancellationToken);
    }

    public interface IAsyncEnumerator<out T> : IAsyncEnumerator
    {
        /// <summary>The current value of the sequence</summary>
        new T Current { get; }
    }

    public static class AsyncLinq
    {
        /// <summary>Skips over the items of at the start of the sequence, returns the nth item onwards</summary>
        public static IAsyncEnumerator<T> Skip<T>(this IAsyncEnumerator<T> source, int skip)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SkipAsyncEnumerator<T>(source, skip);
        }

        /// <summary>Returns first n items in a sequence</summary>
        public static IAsyncEnumerator<T> Take<T>(this IAsyncEnumerator<T> source, int take)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TakeAsyncEnumerator<T>(source, take);
        }

        /// <summary>Filters a sequence using supplied <paramref name="predicate"/></summary>
        public static IAsyncEnumerator<T> Where<T>(this IAsyncEnumerator<T> source, Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new WhereAsyncEnumerator<T>(source, predicate);
        }

        /// <summary>Filters a sequence using supplied async <paramref name="predicate"/></summary>
        public static IAsyncEnumerator<T> WhereAsync<T>(this IEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new WhereAsyncEnumerator2<T>(source.GetEnumerator(), predicate);
        }

        /// <summary>Transforms a sequence from one type to another</summary>
        public static IAsyncEnumerator<TOut> Select<TIn, TOut>(this IAsyncEnumerator<TIn> source, Func<TIn, TOut> transform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SelectAsyncEnumerator<TIn, TOut>(source, transform);
        }        
        
        /// <summary>Transforms a sequence from one type to another via an async <paramref name="transform"/></summary>
        public static IAsyncEnumerator<TOut> SelectAsync<TIn, TOut>(this IEnumerable<TIn> source, Func<TIn, Task<TOut>> transform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SelectAsyncEnumerator2<TIn, TOut>(source.GetEnumerator(), transform);
        }

        /// <summary>Casts each item of a untyped sequence to a known type</summary>
        public static IAsyncEnumerator<T> Cast<T>(this IAsyncEnumerator source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CastAsyncEnumerator<T>(source);
        }

        /// <summary>Returns the first item in the sequence</summary>
        /// <exception cref="InvalidOperationException"> thrown when the sequence is empty</exception>
        public async static Task<T> FirstAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!await source.MoveNextAsync(cancellationToken))
                throw new InvalidOperationException("Sequence is empty");
            return source.Current;
        }

        /// <summary>Returns the first item in the sequence, or the default value if the sequence is empty</summary>
        public async static Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return await source.MoveNextAsync(cancellationToken) ? source.Current : default(T);
        }

        /// <summary>Returns the only item in the sequence</summary>
        /// <exception cref="InvalidOperationException"> thrown when the sequence does not contain exactly one item</exception>
        public async static Task<T> SingleAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!await source.MoveNextAsync(cancellationToken))
                throw new InvalidOperationException("Sequence is empty");
            var result = source.Current;
            if (await source.MoveNextAsync(cancellationToken))
                throw new InvalidOperationException("Sequence contains more than one element");
            return result;
        }

        /// <summary>Returns the only item in the sequence, or the default value if the sequence is empty</summary>
        /// <exception cref="InvalidOperationException"> thrown when the sequence does not contain exactly one item</exception>
        public async static Task<T> SingleOrDefaultAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!await source.MoveNextAsync(cancellationToken))
                return default(T);
            var result = source.Current;
            if (await source.MoveNextAsync(cancellationToken))
                throw new InvalidOperationException("Sequence contains more than one element");
            return result;
        }

        /// <summary>Returns the last item in the sequence</summary>
        /// <exception cref="InvalidOperationException"> thrown when the sequence is empty</exception>
        public async static Task<T> LastAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!await source.MoveNextAsync(cancellationToken))
                throw new InvalidOperationException("Sequence is empty");
            for (;;)
            {
                T result = source.Current;
                if (!await source.MoveNextAsync(cancellationToken))
                    return result;
            }
        }

        /// <summary>Returns the last item in the sequence, or the default value if the sequence is empty</summary>
        public async static Task<T> LastOrDefaultAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!await source.MoveNextAsync(cancellationToken))
                return default(T);
            for (;;)
            {
                T result = source.Current;
                if (!await source.MoveNextAsync(cancellationToken))
                    return result;
            }
        }

        /// <summary>Returns an array containing all the items in the sequence</summary>
        public async static Task<T[]> ToArrayAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            return (await source.ToListAsync(cancellationToken)).ToArray();
        }

        /// <summary>Returns a list containing all the items in the sequence</summary>
        public async static Task<List<T>> ToListAsync<T>(this IAsyncEnumerator<T> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new List<T>();
            while (await source.MoveNextAsync(cancellationToken))
            {
                result.Add(source.Current);
            }
            return result;
        }

        /// <summary>Returns a dictionary containing all the items in the sequence</summary>
        public async static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IAsyncEnumerator<TValue> source, Func<TValue, TKey> keyFunc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new Dictionary<TKey, TValue>();
            while (await source.MoveNextAsync(cancellationToken))
            {
                var key = keyFunc(source.Current);
                result.Add(key, source.Current);
            }
            return result;
        }

        /// <summary>Returns a <see cref="HashLookup{TKey, TElement}"/> containing all the items in the sequence grouped by key</summary>
        public async static Task<HashLookup<TKey, TValue>> ToLookupAsync<TKey, TValue>(this IAsyncEnumerator<TValue> source, Func<TValue, TKey> keyFunc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new HashLookup<TKey, TValue>();
            while (await source.MoveNextAsync(cancellationToken))
            {
                var key = keyFunc(source.Current);
                result.Add(key, source.Current);
            }
            return result;
        }
    }

    class WhereAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        readonly IAsyncEnumerator<T> source;
        readonly Func<T, bool> predicate;

        public WhereAsyncEnumerator(IAsyncEnumerator<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        object IAsyncEnumerator.Current => Current;

        public T Current => source.Current;

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            while (await source.MoveNextAsync(cancellationToken))
            {
                if (predicate(source.Current))
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class WhereAsyncEnumerator2<T> : IAsyncEnumerator<T>
    {
        readonly IEnumerator<T> source;
        readonly Func<T, Task<bool>> predicate;

        public WhereAsyncEnumerator2(IEnumerator<T> source, Func<T, Task<bool>> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        object IAsyncEnumerator.Current => Current;

        public T Current => source.Current;

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            while (source.MoveNext())
            {
                if (await predicate(source.Current))
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class SkipAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        readonly IAsyncEnumerator<T> source;
        int skip;

        public SkipAsyncEnumerator(IAsyncEnumerator<T> source, int skip)
        {
            this.source = source;
            this.skip = skip;
        }

        object IAsyncEnumerator.Current => Current;

        public T Current => source.Current;

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            for (; skip > 0; skip--)
            {
                await source.MoveNextAsync(cancellationToken);
            }

            return await source.MoveNextAsync(cancellationToken);
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class TakeAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        readonly IAsyncEnumerator<T> source;
        int take;

        public TakeAsyncEnumerator(IAsyncEnumerator<T> source, int take)
        {
            this.source = source;
            this.take = take;
        }

        object IAsyncEnumerator.Current => Current;

        public T Current => source.Current;

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            if (take <= 0)
                return Task.FromResult(false);

            take--;
            return source.MoveNextAsync(cancellationToken);
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class SelectAsyncEnumerator<T, TRes> : IAsyncEnumerator<TRes>
    {
        readonly IAsyncEnumerator<T> source;
        readonly Func<T, TRes> transform;

        public SelectAsyncEnumerator(IAsyncEnumerator<T> source, Func<T, TRes> transform)
        {
            this.source = source;
            this.transform = transform;
        }

        object IAsyncEnumerator.Current => Current;

        public TRes Current { get; private set; }

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            if (await source.MoveNextAsync(cancellationToken))
            {
                Current = transform(source.Current);
                return true;
            }
            Current = default(TRes);
            return false;
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class SelectAsyncEnumerator2<T, TRes> : IAsyncEnumerator<TRes>
    {
        readonly IEnumerator<T> source;
        readonly Func<T, Task<TRes>> transform;

        public SelectAsyncEnumerator2(IEnumerator<T> source, Func<T, Task<TRes>> transform)
        {
            this.source = source;
            this.transform = transform;
        }

        object IAsyncEnumerator.Current => Current;

        public TRes Current { get; private set; }

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            if (source.MoveNext())
            {
                Current = await transform(source.Current);
                return true;
            }
            Current = default(TRes);
            return false;
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }

    class CastAsyncEnumerator<TRes> : IAsyncEnumerator<TRes>
    {
        readonly IAsyncEnumerator source;

        public CastAsyncEnumerator(IAsyncEnumerator source)
        {
            this.source = source;
        }

        object IAsyncEnumerator.Current => Current;

        public TRes Current { get; private set; }

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            if (await source.MoveNextAsync(cancellationToken))
            {
                Current = (TRes)source.Current;
                return true;
            }
            Current = default(TRes);
            return false;
        }

        public void Dispose()
        {
            source.Dispose();
        }
    }
}
