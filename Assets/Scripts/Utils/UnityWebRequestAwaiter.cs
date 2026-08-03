using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace BlackjackGame.Utils
{
    /// <summary>
    /// Lets you <c>await</c> a <see cref="UnityWebRequestAsyncOperation"/> directly. Unity
    /// doesn't provide an awaiter out of the box; the continuation resumes on the main
    /// thread via Unity's completion callback, so it's safe to touch Unity objects after.
    /// </summary>
    public static class UnityWebRequestAwaiterExtensions
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation op)
            => new UnityWebRequestAwaiter(op);
    }

    public sealed class UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation _op;
        private Action _continuation;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation op)
        {
            _op = op;
            _op.completed += OnRequestCompleted;
        }

        public bool IsCompleted => _op.isDone;

        public void GetResult() { }

        public void OnCompleted(Action continuation) => _continuation = continuation;

        private void OnRequestCompleted(UnityEngine.AsyncOperation _) => _continuation?.Invoke();
    }
}
