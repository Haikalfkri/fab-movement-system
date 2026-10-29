using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections.Concurrent;

namespace ok1.Services
{
    public static class ImportProgressTracker
    {
        // Key: uploadId, Value: percent (0-100)
        private static ConcurrentDictionary<string, int> _progress = new ConcurrentDictionary<string, int>();

        public static void SetProgress(string uploadId, int percent)
        {
            _progress[uploadId] = percent;
        }

        public static int GetProgress(string uploadId)
        {
            if (_progress.TryGetValue(uploadId, out int percent))
                return percent;
            return 0;
        }

        public static void Remove(string uploadId)
        {
            _progress.TryRemove(uploadId, out _);
        }
    }
}