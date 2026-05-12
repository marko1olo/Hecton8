using System;
using Hecton8.Core;

namespace Hecton8.Narrative
{
    public sealed class LoreEncyclopediaLazyProxy : IServiceShutdown, IDisposable
    {
        private string _indexPath;
        private string _payloadPath;
        private LoreMmfEncyclopedia _runtime;
        private LoreMmfLoadStatus _lastOpenStatus;
        private bool _openAttempted;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_indexPath) && !string.IsNullOrWhiteSpace(_payloadPath);
        public bool IsOpen => _runtime != null && _runtime.IsOpen;
        public LoreMmfLoadStatus LastOpenStatus => _lastOpenStatus;

        public void Configure(string indexPath, string payloadPath)
        {
            DisposeRuntime();
            _indexPath = indexPath;
            _payloadPath = payloadPath;
            _lastOpenStatus = LoreMmfLoadStatus.NotOpen;
            _openAttempted = false;
        }

        public LoreMmfLoadStatus TryLoadEntryUtf16(uint hash, char[] destination, out int charsWritten)
        {
            charsWritten = 0;
            LoreMmfLoadStatus openStatus = EnsureOpen();
            if (openStatus != LoreMmfLoadStatus.Ok)
                return openStatus;

            return _runtime.TryLoadEntryUtf16(hash, destination, out charsWritten);
        }

        public void OnServiceShutdown()
        {
            Dispose();
        }

        public void Dispose()
        {
            DisposeRuntime();
            _indexPath = null;
            _payloadPath = null;
            _lastOpenStatus = LoreMmfLoadStatus.NotOpen;
            _openAttempted = false;
        }

        private LoreMmfLoadStatus EnsureOpen()
        {
            if (_runtime != null && _runtime.IsOpen)
                return LoreMmfLoadStatus.Ok;

            if (_openAttempted)
                return _lastOpenStatus;

            _openAttempted = true;
            if (!IsConfigured)
            {
                _lastOpenStatus = LoreMmfLoadStatus.InvalidPath;
                return _lastOpenStatus;
            }

            _runtime = new LoreMmfEncyclopedia(); // COLD ALLOC: lazy MMF encyclopedia opened only on first user-facing lore request - owner: LoreEncyclopediaLazyProxy
            _lastOpenStatus = _runtime.TryOpen(_indexPath, _payloadPath);
            if (_lastOpenStatus != LoreMmfLoadStatus.Ok)
                DisposeRuntime();

            return _lastOpenStatus;
        }

        private void DisposeRuntime()
        {
            if (_runtime == null)
                return;

            _runtime.Dispose();
            _runtime = null;
        }
    }
}
