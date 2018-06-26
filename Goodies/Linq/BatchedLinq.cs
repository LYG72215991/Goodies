﻿using System;
using System.Collections.Generic;

namespace BusterWood.Linq
{
    public static class BatchedLinq
    {
        /// <summary>Get a <see cref="IBatcher{T}"/> for a data <paramref name="source"/></summary>
        /// <remarks>Optimized for reading <see cref="List{T}"/> or arrays of <typeparamref name="T"/></remarks>
        public static IBatcher<T> Batched<T>(this IEnumerable<T> source, int batchSize = 0)
        {
            if (batchSize <= 0)
                batchSize = BatchSize<T>.Suggested;

            if (source is List<T> list)
                return new ListBatcher<T>(list, batchSize);

            if (source is T[] arr)
                return new ArrayBatcher<T>(arr, batchSize);

            return new EnumerableBatcher<T>(source, batchSize);
        }

        /// <summary>Filters a <paramref name="source"/> using a <paramref name="predicate"/>.  </summary>
        /// <remarks>Lazy evaluated</remarks>
        public static IBatcher<T> Where<T>(this IBatcher<T> source, Func<T, bool> predicate)
        {
            return new WhereBatcher<T>(source, predicate);
        }

        /// <summary>Transforms a <paramref name="source"/> using a <paramref name="mapFunction"/></summary>
        /// <remarks>Lazy evaluated</remarks>
        public static IBatcher<TResult> Select<T, TResult>(this IBatcher<T> source, Func<T, TResult> mapFunction)
        {
            return new SelectBatcher<T, TResult>(source, mapFunction);
        }

        /// <summary>Returns a <see cref="List{T}"/> of all items in the data <paramref name="source"/></summary>
        public static List<T> ToList<T>(this IBatcher<T> source)
        {
            var result = new List<T>();
            for (var batch = source.NextBatch(); batch != default(ArraySegment<T>); batch = source.NextBatch())
            {
                result.Capacity += batch.Count; // ensure list capacity
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    result.Add(arr[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns of a <see cref="Dictionary{TKey, TValue}"/> of all items in the data <paramref name="source"/> 
        /// using the supplied <paramref name="keySelector"/> to generate a <typeparamref name="TKey"/> for each <typeparamref name="TValue"/>.
        /// </summary>
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IBatcher<TValue> source, Func<TValue, TKey> keySelector)
        {
            var result = new Dictionary<TKey, TValue>(source.BatchSize);
            for (var batch = source.NextBatch(); batch != default(ArraySegment<TValue>); batch = source.NextBatch())
            {
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    var key = keySelector(arr[i]);
                    result.Add(key, arr[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns of a <see cref="Dictionary{TKey, TValue}"/> of all items in the data <paramref name="source"/> 
        /// using the supplied <paramref name="keySelector"/> to generate a <typeparamref name="TKey"/> for each <typeparamref name="T"/>
        /// and the <paramref name="valueSelector"/> to transform the into <typeparamref name="T"/> into a <typeparamref name="TValue"/>.
        /// </summary>
        public static Dictionary<TKey, TValue> ToDictionary<T, TKey, TValue>(this IBatcher<T> source, Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            var result = new Dictionary<TKey, TValue>(source.BatchSize);
            for (var batch = source.NextBatch(); batch != default(ArraySegment<T>); batch = source.NextBatch())
            {
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    var key = keySelector(arr[i]);
                    var value = valueSelector(arr[i]);
                    result.Add(key, value);
                }
            }
            return result;
        }

        /// <summary>Do any of the <paramref name="source"/> items match the <paramref name="predicate"/>?</summary>
        /// <remarks>Stops at the first matching value</remarks>
        public static bool Any<T>(this IBatcher<T> source, Func<T, bool> predicate)
        {
            for (var batch = source.NextBatch(); batch != default(ArraySegment<T>); batch = source.NextBatch())
            {
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    if (predicate(arr[i]))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Do all of the <paramref name="source"/> items match the <paramref name="predicate"/>?</summary>
        /// <remarks>Stops at the first value that does not match</remarks>
        public static bool All<T>(this IBatcher<T> source, Func<T, bool> predicate)
        {
            for (var batch = source.NextBatch(); batch != default(ArraySegment<T>); batch = source.NextBatch())
            {
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    if (!predicate(arr[i]))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Aggregate (fold) all the items from the <paramref name="source"/> calling the supplied <paramref name="accumulate"/>
        /// function for each <typeparamref name="T"/> and passing the <typeparamref name="TAcc"/> generated by the last call.
        /// The first call to <paramref name="accumulate"/> gets passed the <paramref name="intial"/> value.
        /// </summary>
        public static TAcc Aggregate<T, TAcc>(this IBatcher<T> source, TAcc intial, Func<T, TAcc, TAcc> accumulate)
        {
            var result = intial;
            for (var batch = source.NextBatch(); batch != default(ArraySegment<T>); batch = source.NextBatch())
            {
                var arr = batch.Array;
                int start = batch.Offset;
                int end = batch.Count + batch.Offset;
                for (int i = start; i < end; i++)
                {
                    result = accumulate(arr[i], result);
                }
            }
            return result;
        }

    }
}