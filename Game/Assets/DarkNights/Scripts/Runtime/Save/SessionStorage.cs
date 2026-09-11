using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Runtime.Session;

namespace DarkNights.Runtime.Save
{
    /// <summary>
    /// 单个房主会话的文件任务生命周期；后台只序列化冻结数据或读取文本，完成时由服务端 Update 回收。
    /// 同时最多一个任务，退出取消后不向已关闭的世界提交；文件失败不替换现有世界或旧存档。
    /// </summary>
    public sealed class SessionStorage : IDisposable
    {
        private readonly SessionAuthority authority;
        private readonly GameSaveStore store;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private SessionStorageRequest current;
        private Task<string> task;
        private bool disposed;
        public string Status { get; private set; } = "";
        public bool Busy => current != null || authority.StorageRequest != null;
        public event Action<bool, string> Completed;

        public SessionStorage(SessionAuthority authority, GameSaveStore store)
        {
            this.authority = authority;
            this.store = store;
        }

        public void Advance()
        {
            if (disposed || authority.Closed) return;
            if (current == null && authority.StorageRequest != null)
            {
                current = authority.StorageRequest;
                Status = current.Operation == SessionOperation.Save ? "正在保存营地……" : "正在载入营地……";
                SessionStorageRequest frozen = current;
                CancellationToken token = cancellation.Token;
                task = Task.Run(() =>
                {
                    if (frozen.Operation == SessionOperation.Save) { store.Save(frozen.Slot, frozen.Snapshot, token); return ""; }
                    return frozen.Operation == SessionOperation.Restart ? "" : store.Read(frozen.Slot, token);
                }, token);
            }
            if (task == null || !task.IsCompleted) return;
            bool success = false;
            try
            {
                string json = task.GetAwaiter().GetResult();
                if (current.Operation == SessionOperation.Restart) authority.CompleteRestart(current.Ticket);
                else if (current.Operation == SessionOperation.BeginLoad) authority.CompleteLoad(current.Ticket, json);
                Status = current.Operation == SessionOperation.Save ? $"营地已保存到槽位 {current.Slot + 1}。"
                    : current.Operation == SessionOperation.Restart ? "新的营地已就绪。" : $"已载入槽位 {current.Slot + 1}。";
                success = true;
            }
            catch (Exception exception)
            {
                if (authority.Loading) authority.CancelLoad(current.Ticket);
                Status = exception is System.IO.FileNotFoundException ? "该槽位没有存档，请选择其他槽位。"
                    : "存档操作失败，请检查文件完整性及存储权限。";
            }
            finally
            {
                authority.ReleaseStorage(current);
                current = null;
                task = null;
            }
            Completed?.Invoke(success, Status);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation.Cancel();
            if (task != null) _ = task.ContinueWith(t => { _ = t.Exception; cancellation.Dispose(); });
            else cancellation.Dispose();
            current = null;
            task = null;
            Completed = null;
        }
    }
}
