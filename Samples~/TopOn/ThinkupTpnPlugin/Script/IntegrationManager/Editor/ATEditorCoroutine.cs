using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace AnyThink.Scripts.IntegrationManager.Editor
{
    public class ATEditorCoroutine
    {
        private readonly Stack<IEnumerator> coroutineStack = new Stack<IEnumerator>();
        private bool isRunning;

        private ATEditorCoroutine(IEnumerator routine)
        {
            coroutineStack.Push(routine);
        }

        public static ATEditorCoroutine startCoroutine(IEnumerator routine)
        {
            if (routine == null) throw new ArgumentNullException("routine");

            var instance = new ATEditorCoroutine(routine);
            instance.Activate();
            return instance;
        }

        private void Activate()
        {
            if (isRunning) return;
            isRunning = true;
            EditorApplication.update += Tick;
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            EditorApplication.update -= Tick;
            coroutineStack.Clear();
        }

        private void Tick()
        {
            if (coroutineStack.Count == 0)
            {
                Stop();
                return;
            }

            var current = coroutineStack.Peek();
            bool advanced;
            try
            {
                advanced = current.MoveNext();
            }
            catch (Exception)
            {
                Stop();
                throw;
            }

            if (advanced)
            {
                var nested = current.Current as IEnumerator;
                if (nested != null)
                {
                    coroutineStack.Push(nested);
                }
            }
            else
            {
                coroutineStack.Pop();
                if (coroutineStack.Count == 0)
                {
                    Stop();
                }
            }
        }
    }
}
