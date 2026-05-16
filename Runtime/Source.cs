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
        public bool IsValid() => _instanceId.IsValid() && _generation.IsValid();
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

    public readonly struct HandleSource {
        private readonly InstanceId _instanceId;
        private readonly IGenerationSource _generationSource;
        internal HandleSource(
            InstanceId instanceId,
            IGenerationSource generationSource) {

            if (instanceId.IsValid() && generationSource.IsValid()) {
                _instanceId = instanceId;
                _generationSource = generationSource;
                return;
            }

            throw new InvalidOperationException("HandleSource の作成に失敗しました。");
        }
        public bool EqualsHandle(Handle handle) {

            // ここでは HandleSource._generationSource の等価性は比較しない。
            // HandleSource のコンストラクタがアセンブリ内でしか呼び出せないことと、
            // その呼び出しを HandleAuthority.Issue メソッドのみが行うことによって
            // HandleSource._instanceId と Handle._generationSource の組み合わせの整合性を保証する。

            var generation = _generationSource.GetGeneration();
            return _instanceId == handle.GetInstanceId() && generation == handle.GetGeneration();
        }
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
        public HandleSource Issue(out Handle handle) {

            var instanceId = IssueInstanceId();
            var generationSource = new GenerationSource();
            var generation = generationSource.GetGeneration();
            var handleSource = new HandleSource(instanceId, generationSource);

            handle = new(instanceId, generation);

            _generationSources.Add(instanceId, generationSource);

            return handleSource;
        }

        public bool TryAdvance(Handle current, out Handle next) {

            var instanceId = current.GetInstanceId();

            if (_generationSources.TryGetValue(instanceId, out var generationSource)) {
                if (current.GetGeneration() == generationSource.GetGeneration()) {

                    if (generationSource.Advance(out var newGeneration)) {
                        next = new(instanceId, newGeneration);
                        return true;
                    }

                    _generationSources.Remove(instanceId);
                    next = default;
                    return false;
                }

                throw new InvalidOperationException("ハンドルの開放に失敗しました。このハンドルは開放済みです。");
            } else {
                throw new InvalidOperationException("ハンドルの開放に失敗しました。不明なインスタンス ID です。");
            }
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
