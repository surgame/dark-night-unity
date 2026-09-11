namespace DarkNights.Core.Logic.State
{
    /// <summary>
    /// 表示当前战局允许的营地控制策略。该值由权威会话持有；客户端投影只能展示，
    /// SharedCamp 允许所有已验证玩家操作共享营地，HostOnly 仅允许房主提交营地修改命令。
    /// </summary>
    public enum CampControlMode
    {
        SharedCamp = 0,
        HostOnly = 1
    }
}
