using System;
using System.Collections.Generic;

namespace Viyiw.Handles {
    internal readonly struct InstanceId : IEquatable<InstanceId> {
        private readonly int _value;
        internal InstanceId(int value) { _value = value; }
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
        internal Generation(uint value) {
            _value = value;
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

    internal interface IGenerationSource {
        internal Generation GetGeneration();
    }

    public readonly struct HandleIdentity {
        private readonly InstanceId _instanceId;
        private readonly IGenerationSource _generationSource;
        internal HandleIdentity(
            InstanceId instanceId,
            IGenerationSource generationSource) {
            _instanceId = instanceId;
            _generationSource = generationSource;
        }
        public GenericHandle ToHandle() {
            if (GenericHandle.TryNew(_instanceId, _generationSource.GetGeneration(), out var newHandle)) {
                return newHandle;
            }

            // インスタンス ID または世代 ID が不正な場合は例外を送出する。
            throw new InvalidOperationException("ハンドルの作成に失敗しました。");
        }
        public bool EqualsHandle(GenericHandle inHandle) {
            var generation = _generationSource.GetGeneration();
            if (GenericHandle.TryNew(_instanceId, generation, out var handle)) {
                return handle.Equals(inHandle);
            }
            throw new InvalidOperationException();
        }
    }

    public sealed class HandleAuthority {
        private sealed class GenerationSource : IGenerationSource {
            private uint _generation = 1;
            public Generation GetGeneration() => new Generation(_generation);
            public bool TryIncrement() {
                if (_generation == uint.MaxValue) {
                    return false;
                }
                _generation++;
                return true;
            }
        }

        private int _lastInstanceId;
        private readonly Dictionary<InstanceId, GenerationSource> _generationSources = new();
        private InstanceId IssueInstanceId() {
            checked {
                _lastInstanceId++;
            }
            return new InstanceId(_lastInstanceId);
        }
        public HandleIdentity Issue(out GenericHandle handle) {

            var instanceId = IssueInstanceId();
            var generationSource = new GenerationSource();
            var generation = generationSource.GetGeneration();

            if (GenericHandle.TryNew(instanceId, generation, out handle)) {

                // ハンドルの作成に成功した場合のみ
                // GenerationSource を _generationSources に登録する。
                _generationSources.Add(instanceId, generationSource);

                return new(instanceId, generationSource);
            }

            throw new InvalidOperationException("ハンドルの作成に失敗しました。");
        }

        // Release という名前は適切ではないかも。

        public bool Release(GenericHandle handle) {

            if (_generationSources.TryGetValue(handle.InstanceId, out var generationSource)) {
                if (handle.Generation == generationSource.GetGeneration()) {
                    if (generationSource.TryIncrement()) {
                        return true;
                    }

                    _generationSources.Remove(handle.InstanceId);
                    return false;
                }

                throw new InvalidOperationException("ハンドルの開放に失敗しました。このハンドルは開放済みです。");
            } else {
                throw new InvalidOperationException("ハンドルの開放に失敗しました。不明なインスタンス ID です。");
            }
        }
    }

    public readonly struct GenericHandle : IEquatable<GenericHandle> {
        internal readonly InstanceId InstanceId;
        internal readonly Generation Generation;
        private GenericHandle(InstanceId instanceId, Generation generation) {
            InstanceId = instanceId;
            Generation = generation;
        }
        internal static bool TryNew(InstanceId instanceId, Generation generation, out GenericHandle newHandle) {
            if (instanceId.IsValid() && generation.IsValid()) {
                newHandle = new GenericHandle(instanceId, generation);
                return true;
            }

            newHandle = default;
            return false;
        }
        public bool IsValid() => InstanceId.IsValid() && Generation.IsValid();
        public bool Equals(GenericHandle other) {
            return InstanceId == other.InstanceId && Generation == other.Generation;
        }
        public override bool Equals(object obj) {
            return obj is GenericHandle other && Equals(other);
        }
        public override int GetHashCode() {
            return HashCode.Combine(InstanceId.GetHashCode(), Generation.GetHashCode());
        }
        public static bool operator ==(GenericHandle left, GenericHandle right) {
            return left.InstanceId == right.InstanceId && left.Generation == right.Generation;
        }
        public static bool operator !=(GenericHandle left, GenericHandle right) {
            return left.InstanceId != right.InstanceId || left.Generation != right.Generation;
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
        private int _last;
        public ArchetypeId Issue() {
            checked {
                _last++;
            }
            return new(_last);
        }
    }
}
