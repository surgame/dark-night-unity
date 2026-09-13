using System;
using System.Collections.Generic;
using GameCore.Objects.Runner;
using GameCore.Objects.NetworkStates;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 主线程内协调多个 YYGC 状态所有者的短事务；检查和准备先于发布，禁止 await 及订阅重入。
    /// 只记录本次触及的能力、创建回滚和提交后反馈，不长期持有第二份业务状态。
    /// </summary>
    public sealed class ObjectMutationBatch
    {
        private readonly ObjectSessionContext context;
        private readonly List<IObjectMutation> touched = new List<IObjectMutation>();
        private readonly List<Action> rollback = new List<Action>();
        private readonly List<Action> committed = new List<Action>();
        public bool IsOpen { get; private set; }
        internal bool Publishing { get; private set; }

        public ObjectMutationBatch(ObjectSessionContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public T Run<T>(Func<T> action)
        {
            context.RequireAvailable();
            if (!context.CanWriteState || IsOpen)
                throw new InvalidOperationException("Only the current authority may start a non-reentrant object transaction.");
            IsOpen = true;
            bool success = false;
            Action[] notifications = null;
            try
            {
                T result = action();
                var changes = new List<SessionStateChange>(touched.Count);
                foreach (IObjectMutation item in touched) changes.Add(item.PrepareCommit());
                Publishing = true;
                SessionStateChange.CommitAll(changes);
                success = true;
                notifications = committed.ToArray();
                return result;
            }
            finally
            {
                try
                {
                    foreach (IObjectMutation item in touched)
                    {
                        try { item.Complete(); }
                        catch (Exception error) { Debug.LogException(error); }
                    }
                    if (!success)
                        for (int index = rollback.Count - 1; index >= 0; index--)
                        {
                            try { rollback[index](); }
                            catch (Exception error) { Debug.LogException(error); }
                        }
                }
                finally
                {
                    touched.Clear();
                    rollback.Clear();
                    committed.Clear();
                    Publishing = false;
                    IsOpen = false;
                }
                if (success)
                    foreach (Action notification in notifications) notification();
            }
        }

        internal void RequireWriting()
        {
            context.RequireAvailable();
            if (!context.CanWriteState || !IsOpen || Publishing)
                throw new InvalidOperationException("Gameplay writes require the current synchronous object transaction.");
        }

        internal void Touch(IObjectMutation owner)
        {
            RequireWriting();
            touched.Add(owner);
        }

        internal void OnRollback(Action action)
        {
            RequireWriting();
            rollback.Add(action);
        }

        internal void AfterCommit(Action action)
        {
            RequireWriting();
            committed.Add(action);
        }
    }
}
