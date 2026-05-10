using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.Core
{
    public delegate bool NativePredicate<T>(T value) where T : unmanaged;

    public delegate TResult NativeSelector<TSource, TResult>(TSource value)
        where TSource : unmanaged
        where TResult : unmanaged;

    public readonly struct NativeQuery<T> where T : unmanaged
    {
        internal readonly NativeArray<T> Source;
        internal readonly FunctionPointer<NativePredicate<T>> Predicate;

        internal NativeQuery(NativeArray<T> source, FunctionPointer<NativePredicate<T>> predicate)
        {
            Source = source;
            Predicate = predicate;
        }

        public bool IsCreated => Source.IsCreated && Predicate.IsCreated;
        public int Length => Source.IsCreated ? Source.Length : 0;

        public NativeFilterJob<T> CreateFilterJob(NativeList<T> output)
        {
            return new NativeFilterJob<T>
            {
                Source = Source,
                Predicate = Predicate,
                Output = output
            };
        }
    }

    public readonly struct NativeSelectQuery<TSource, TResult>
        where TSource : unmanaged
        where TResult : unmanaged
    {
        internal readonly NativeArray<TSource> Source;
        internal readonly FunctionPointer<NativeSelector<TSource, TResult>> Selector;

        internal NativeSelectQuery(
            NativeArray<TSource> source,
            FunctionPointer<NativeSelector<TSource, TResult>> selector)
        {
            Source = source;
            Selector = selector;
        }

        public bool IsCreated => Source.IsCreated && Selector.IsCreated;
        public int Length => Source.IsCreated ? Source.Length : 0;

        public NativeSelectJob<TSource, TResult> CreateSelectJob(NativeArray<TResult> output)
        {
            return new NativeSelectJob<TSource, TResult>
            {
                Source = Source,
                Selector = Selector,
                Output = output
            };
        }
    }

    public static class NativeQueryExtensions
    {
        public static NativeList<T> Where<T>(
            this NativeArray<T> source,
            FunctionPointer<NativePredicate<T>> predicate,
            Allocator allocator = Allocator.TempJob) where T : unmanaged
        {
            NativeList<T> output = new NativeList<T>(source.IsCreated ? source.Length : 0, allocator);
            if (!source.IsCreated || !predicate.IsCreated || source.Length <= 0)
                return output;

            new NativeFilterJob<T>
            {
                Source = source,
                Predicate = predicate,
                Output = output
            }.Schedule().Complete();

            return output;
        }

        public static NativeList<TResult> Select<TSource, TResult>(
            this NativeArray<TSource> source,
            FunctionPointer<NativeSelector<TSource, TResult>> selector,
            Allocator allocator = Allocator.TempJob)
            where TSource : unmanaged
            where TResult : unmanaged
        {
            NativeList<TResult> output = new NativeList<TResult>(source.IsCreated ? source.Length : 0, allocator);
            if (!source.IsCreated || !selector.IsCreated || source.Length <= 0)
                return output;

            output.ResizeUninitialized(source.Length);
            new NativeSelectJob<TSource, TResult>
            {
                Source = source,
                Selector = selector,
                Output = output.AsArray()
            }.Schedule().Complete();

            return output;
        }

        public static NativeQuery<T> WhereQuery<T>(
            this NativeArray<T> source,
            FunctionPointer<NativePredicate<T>> predicate) where T : unmanaged
        {
            return new NativeQuery<T>(source, predicate);
        }

        public static NativeSelectQuery<TSource, TResult> SelectQuery<TSource, TResult>(
            this NativeArray<TSource> source,
            FunctionPointer<NativeSelector<TSource, TResult>> selector)
            where TSource : unmanaged
            where TResult : unmanaged
        {
            return new NativeSelectQuery<TSource, TResult>(source, selector);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct NativeFilterJob<T> : IJob where T : unmanaged
    {
        [ReadOnly] public NativeArray<T> Source;
        public FunctionPointer<NativePredicate<T>> Predicate;
        public NativeList<T> Output;

        public void Execute()
        {
            if (!Source.IsCreated || !Predicate.IsCreated || !Output.IsCreated)
                return;

            for (int i = 0; i < Source.Length; i++)
            {
                T value = Source[i];
                if (Predicate.Invoke(value) && Output.Length < Output.Capacity)
                    Output.AddNoResize(value);
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct NativeSelectJob<TSource, TResult> : IJob
        where TSource : unmanaged
        where TResult : unmanaged
    {
        [ReadOnly] public NativeArray<TSource> Source;
        public FunctionPointer<NativeSelector<TSource, TResult>> Selector;
        public NativeArray<TResult> Output;

        public void Execute()
        {
            if (!Source.IsCreated || !Selector.IsCreated || !Output.IsCreated)
                return;

            int count = Source.Length < Output.Length ? Source.Length : Output.Length;
            for (int i = 0; i < count; i++)
                Output[i] = Selector.Invoke(Source[i]);
        }
    }
}
