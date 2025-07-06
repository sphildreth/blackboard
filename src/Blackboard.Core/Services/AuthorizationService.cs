using Blackboard.Core.Models;

namespace Blackboard.Core.Services;

public interface IAuthorizationService
{
    bool HasPermission(SecurityLevel userLevel, SecurityLevel requiredLevel);
    bool CanAccessAdminPanel(SecurityLevel userLevel);
    bool CanManageUsers(SecurityLevel userLevel);
    bool CanViewAuditLogs(SecurityLevel userLevel);
    bool CanModerateContent(SecurityLevel userLevel);
    bool CanManageSystem(SecurityLevel userLevel);
    bool CanAccessFileAreas(SecurityLevel userLevel, SecurityLevel? areaMinLevel = null);
    bool CanUploadFiles(SecurityLevel userLevel);
    bool CanDownloadFiles(SecurityLevel userLevel);
    bool CanSendPrivateMessages(SecurityLevel userLevel);
    bool CanPostPublicMessages(SecurityLevel userLevel);
    bool CanAccessDoorGames(SecurityLevel userLevel);
    bool CanKickUsers(SecurityLevel userLevel);
    bool CanBanUsers(SecurityLevel userLevel);
}

public class AuthorizationService : IAuthorizationService
{
    public bool HasPermission(SecurityLevel userLevel, SecurityLevel requiredLevel)
    {
        return (int)userLevel >= (int)requiredLevel;
    }

    public bool CanAccessAdminPanel(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Moderator);
    }

    public bool CanManageUsers(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.CoSysop);
    }

    public bool CanViewAuditLogs(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Moderator);
    }

    public bool CanModerateContent(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Moderator);
    }

    public bool CanManageSystem(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Sysop);
    }

    public bool CanAccessFileAreas(SecurityLevel userLevel, SecurityLevel? areaMinLevel = null)
    {
        if (userLevel == SecurityLevel.Banned)
            return false;

        if (areaMinLevel.HasValue)
            return HasPermission(userLevel, areaMinLevel.Value);

        return HasPermission(userLevel, SecurityLevel.User);
    }

    public bool CanUploadFiles(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Trusted);
    }

    public bool CanDownloadFiles(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.User);
    }

    public bool CanSendPrivateMessages(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.User);
    }

    public bool CanPostPublicMessages(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.User);
    }

    public bool CanAccessDoorGames(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.User);
    }

    public bool CanKickUsers(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.Moderator);
    }

    public bool CanBanUsers(SecurityLevel userLevel)
    {
        return HasPermission(userLevel, SecurityLevel.CoSysop);
    }
}