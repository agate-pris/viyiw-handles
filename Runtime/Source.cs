using System;
using System.Collections.Generic;

namespace Viyiw.Handles {

    internal readonly struct InstanceId : IEquatable<InstanceId> {
        private readonly int _value;
        public InstanceId(int value) {

            if (0 < value) {
                _value = value;
                return;
            }

            throw new InvalidOperationException("InstanceId の値は正の整数でなければなりません。");
        }
        public bool IsValid() => 0 < _value;
        public bool Equals(InstanceId other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is InstanceId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(InstanceId left, InstanceId right) {
            return left._value == right._value;
        }
        public static bool operator !=(InstanceId left, InstanceId right) {
            return left._value != right._value;
        }
    }

    internal readonly struct Generation : IEquatable<Generation> {
        private readonly uint _value;
        public Generation(uint value) {

            if (0 < value) {
                _value = value;
                return;
            }

            throw new InvalidOperationException("Generation の値は正の整数でなければなりません。");
        }
        public bool IsValid() => 0 < _value;
        public bool Equals(Generation other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is Generation other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(Generation left, Generation right) {
            return left._value == right._value;
        }
        public static bool operator !=(Generation left, Generation right) {
            return left._value != right._value;
        }
    }

    public readonly struct Handle : IEquatable<Handle> {
        private readonly InstanceId _instanceId;
        private readonly Generation _generation;
        internal Handle(InstanceId instanceId, Generation generation) {
            if (instanceId.IsValid() && generation.IsValid()) {
                _instanceId = instanceId;
                _generation = generation;
                return;
            }
            throw new InvalidOperationException("ハンドルの作成に失敗しました。");
        }
        internal InstanceId GetInstanceId() => _instanceId;
        internal Generation GetGeneration() => _generation;
        internal bool IsValid() => _instanceId.IsValid() && _generation.IsValid();
        public bool Equals(Handle other) {
            return _instanceId == other._instanceId && _generation == other._generation;
        }
        public override bool Equals(object obj) {
            return obj is Handle other && Equals(other);
        }
        public override int GetHashCode() {
            return HashCode.Combine(_instanceId.GetHashCode(), _generation.GetHashCode());
        }
        public static bool operator ==(Handle left, Handle right) {
            return left._instanceId == right._instanceId && left._generation == right._generation;
        }
        public static bool operator !=(Handle left, Handle right) {
            return left._instanceId != right._instanceId || left._generation != right._generation;
        }
    }

    internal interface IGenerationSource {
        internal bool IsValid();
        Generation GetGeneration();
    }

    public sealed class HandleAuthority {

        private sealed class GenerationSource : IGenerationSource {
            private uint _generation = 1;
            bool IGenerationSource.IsValid() => 0 < _generation;
            public Generation GetGeneration() => new(_generation);
            public bool Advance(out Generation newGeneration) {
                if (_generation == uint.MaxValue) {
                    newGeneration = default;
                    return false;
                }

                newGeneration = new(++_generation);

                // もし uint.MaxValue を有効な値として使えるようにしてしまうと
                // その世代の二重開放を検出できなくなってしまうため、
                // _generation が uint.MaxValue に到達した時点で無効な状態にする。

                return _generation != uint.MaxValue;
            }
        }

        private int _lastInstanceId = 0;
        private readonly Dictionary<InstanceId, GenerationSource> _generationSources = new();

        private InstanceId IssueInstanceId() {
            checked {
                _lastInstanceId++;
            }
            return new(_lastInstanceId);
        }
        public Handle Issue() {

            var instanceId = IssueInstanceId();
            var generationSource = new GenerationSource();
            var generation = generationSource.GetGeneration();
            var handle = new Handle(instanceId, generation);

            _generationSources.Add(instanceId, generationSource);

            return handle;
        }

        internal bool IsAlive(Handle handle) {

            // Pool との組み合わせの不一致を防止するため internal にする。

            var instanceId = handle.GetInstanceId();

            if (_generationSources.TryGetValue(instanceId, out var generationSource)) {
                return handle.GetGeneration() == generationSource.GetGeneration();
            } else {
                throw new InvalidOperationException("ハンドルの有効期限の確認に失敗しました。不明なインスタンス ID です。");
            }
        }
        public bool TryAdvance(Handle currentHandle, out Handle nextHandle) {

            var instanceId = currentHandle.GetInstanceId();

            if (_generationSources.TryGetValue(instanceId, out var generationSource)) {

                if (currentHandle.GetGeneration() == generationSource.GetGeneration()) {

                    if (generationSource.Advance(out var newGeneration)) {
                        nextHandle = new(instanceId, newGeneration);
                        return true;
                    }

                    _generationSources.Remove(instanceId);
                    nextHandle = default;
                    return false;
                }

                throw new InvalidOperationException("ハンドルの開放に失敗しました。このハンドルは開放済みです。");
            }

            throw new InvalidOperationException("ハンドルの開放に失敗しました。不明なインスタンス ID です。");
        }
    }


    public readonly struct Lease<T> {
        private readonly Pool<T> _owner;
        private readonly Handle _handle;
        private readonly T _value;
        internal Lease(Pool<T> owner, Handle handle, T value) {

            if (owner == null) {
                throw new InvalidOperationException("Lease の作成に失敗しました。所有者が null です。");
            }

            if (handle.IsValid()) {
                _owner = owner;
                _handle = handle;
                _value = value;
                return;
            }

            throw new InvalidOperationException("Lease の作成に失敗しました。ハンドルが無効です。");
        }
        internal Pool<T> GetOwner() => _owner;
        public Handle GetHandle() => _handle;
        public T GetValue() => _value;
        public bool IsAlive() {

            if (_owner == null) {
                return false;
            }

            // _handle が無効になるのはデフォルト値に対してのみであり、
            // _owner が null でない限り _owner と _handle の組み合わせが不正になることはない。
            // したがって、ここでは _handle 自身の有効性は確認は不要。

            return _owner.IsAlive(_handle);
        }
        public void Release() {
            _owner.Release(this);
        }
    }

    public interface IFactory<T> {
        T Create();
    }

    public class Pool<T> {

        private readonly struct Entry {

            private readonly Handle _nextHandle;
            private readonly T _value;

            public Entry(Handle handle, T value) {
                _nextHandle = handle;
                _value = value;
            }

            public Lease<T> Lease(Pool<T> owner) => new(owner, _nextHandle, _value);
        }

        private readonly HandleAuthority _handleAuthority;
        private readonly IFactory<T> _factory;
        private readonly Stack<Entry> _available = new();
        public Pool(HandleAuthority handleAuthority, IFactory<T> factory) {
            _handleAuthority = handleAuthority;
            _factory = factory;
        }

        internal bool IsAlive(Handle handle) {

            // handle の有効性の確認は不要。
            // このメソッドが呼び出される以前に
            // _handleAuthority と handle の組み合わせの有効性は確認されているはず。
            // そして、その時点で Handle の有効性も保証されているはず。

            return _handleAuthority.IsAlive(handle);
        }
        internal void Release(Lease<T> lease) {

            // 世代の寿命が尽きた場合はオブジェクトをプールしない。

            if (_handleAuthority.TryAdvance(lease.GetHandle(), out var nextHandle)) {
                _available.Push(new(nextHandle, lease.GetValue()));
            }
        }
        public Lease<T> Get() {

            if (_available.TryPop(out var entry)) {
                return entry.Lease(this);
            }

            var handle = _handleAuthority.Issue();
            var value = _factory.Create();

            return new(this, handle, value);
        }
    }

    public readonly struct ArchetypeId : IEquatable<ArchetypeId> {
        private readonly int _value;
        internal ArchetypeId(int value) { _value = value; }
        public bool IsValid() => 0 < _value;
        public bool Equals(ArchetypeId other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is ArchetypeId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(ArchetypeId left, ArchetypeId right) {
            return left._value == right._value;
        }
        public static bool operator !=(ArchetypeId left, ArchetypeId right) {
            return left._value != right._value;
        }
    }

    public sealed class ArchetypeIdIssuer {
        private int _last = 0;
        public ArchetypeId Issue() {
            checked {
                _last++;
            }
            return new(_last);
        }
    }
}
