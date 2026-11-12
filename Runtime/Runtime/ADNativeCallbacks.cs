using System;
using System.Collections.Concurrent;
using GameFrameX.Runtime;

namespace GameFrameX.Ad.Runtime
{
    // SDK callbacks are drained by the existing GameFrameX module update.
    internal sealed class ADNativeCallbacks
    {
        private readonly ConcurrentQueue<Action> pending = new ConcurrentQueue<Action>();
        private volatile bool active;

        public void Initialize() { active = true; }
        public void Shutdown()
        {
            active = false;
            while (pending.TryDequeue(out _)) { }
        }
        public void Enqueue(Action callback)
        {
            if (active) pending.Enqueue(callback);
        }
        public void Dispatch()
        {
            while (pending.TryDequeue(out var callback))
            {
                if (!active) continue;
                try { callback(); }
                catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }
        }

        public static void Publish<T>(object sender, EventHandler<T> handler, T args)
            where T : EventArgs, IReference
        {
            try { handler?.Invoke(sender, args); }
            finally { ReferencePool.Release(args); }
        }
    }
}
