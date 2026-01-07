
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Text;

namespace JiebaNet
{

    internal class StringBuilderPool
    {
        private static readonly Lazy<DefaultObjectPool<StringBuilder>> lazyInstance = new Lazy<DefaultObjectPool<StringBuilder>>(() => new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy()));
        public static DefaultObjectPool<StringBuilder> Instance => lazyInstance.Value;
        class StringBuilderPooledObjectPolicy : IPooledObjectPolicy<StringBuilder>
        {
            public int MaximumRetainedCapacity { get; set; } = 4 * 1024;
            public StringBuilder Create() => new StringBuilder(64);
            public bool Return(StringBuilder obj)
            {
                if (obj.Capacity > MaximumRetainedCapacity)
                {
                    return false;
                }
                obj.Clear();
                return true;
            }
        }
    }

    /// <summary>
    /// Default implementation of ObjectPool
    /// </summary>
    /// <typeparam name="T">The type to pool objects for.</typeparam>
    /// <remarks>This implementation keeps a cache of retained objects. This means that if objects are returned when the pool has already reached "maximumRetained" objects they will be available to be Garbage Collected.</remarks>
    internal class DefaultObjectPool<T> where T : class
    {
        private readonly Func<T> _createFunc;
        private readonly Func<T, bool> _returnFunc;
        private readonly int _maxCapacity;
        private int _numItems;

        private protected readonly ConcurrentQueue<T> _items = new();
        private protected T _fastItem;

        /// <summary>
        /// Creates an instance of <see cref="DefaultObjectPool{T}"/>.
        /// </summary>
        /// <param name="policy">The pooling policy to use.</param>
        public DefaultObjectPool(IPooledObjectPolicy<T> policy)
            : this(policy, Environment.ProcessorCount * 2)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="DefaultObjectPool{T}"/>.
        /// </summary>
        /// <param name="policy">The pooling policy to use.</param>
        /// <param name="maximumRetained">The maximum number of objects to retain in the pool.</param>
        public DefaultObjectPool(IPooledObjectPolicy<T> policy, int maximumRetained)
        {
            // cache the target interface methods, to avoid interface lookup overhead
            _createFunc = policy.Create;
            _returnFunc = policy.Return;
            _maxCapacity = maximumRetained - 1;  // -1 to account for _fastItem
        }

        /// <inheritdoc />
        public T Get()
        {
            var item = _fastItem;
            if (item == null || Interlocked.CompareExchange(ref _fastItem, null, item) != item)
            {
                if (_items.TryDequeue(out item))
                {
                    Interlocked.Decrement(ref _numItems);
                    return item;
                }

                // no object available, so go get a brand new one
                return _createFunc();
            }

            return item;
        }

        /// <inheritdoc />
        public void Return(T obj)
        {
            ReturnCore(obj);
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <returns>true if the object was returned to the pool</returns>
        private protected bool ReturnCore(T obj)
        {
            if (!_returnFunc(obj))
            {
                // policy says to drop this object
                return false;
            }

            if (_fastItem != null || Interlocked.CompareExchange(ref _fastItem, obj, null) != null)
            {
                if (Interlocked.Increment(ref _numItems) <= _maxCapacity)
                {
                    _items.Enqueue(obj);
                    return true;
                }

                // no room, clean up the count and drop the object on the floor
                Interlocked.Decrement(ref _numItems);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Represents a policy for managing pooled objects.
    /// </summary>
    /// <typeparam name="T">The type of object which is being pooled.</typeparam>
    internal interface IPooledObjectPolicy<T> where T : notnull
    {
        /// <summary>
        /// Create a <typeparamref name="T"/>.
        /// </summary>
        /// <returns>The <typeparamref name="T"/> which was created.</returns>
        T Create();

        /// <summary>
        /// Runs some processing when an object was returned to the pool. Can be used to reset the state of an object and indicate if the object should be returned to the pool.
        /// </summary>
        /// <param name="obj">The object to return to the pool.</param>
        /// <returns><see langword="true" /> if the object should be returned to the pool. <see langword="false" /> if it's not possible/desirable for the pool to keep the object.</returns>
        bool Return(T obj);
    }
}