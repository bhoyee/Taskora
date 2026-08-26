namespace TodoApp.Application.PublicDemo;

/// <summary>
/// Fixed identifiers for the public "View demo" workspace and its four role
/// personas. This is the single source of truth both
/// <c>PublicDemoSeeder</c> (Infrastructure, which creates this data) and the
/// destructive-action guard below (used by handlers in this layer) rely on,
/// so they can never drift apart and disagree about which account/workspace
/// this refers to.
/// </summary>
public static class PublicDemoIdentifiers
{
    public static readonly Guid WorkspaceId =
        Guid.Parse("c2000000-0000-0000-0000-000000000001");
    public static readonly Guid OwnerId =
        Guid.Parse("c1000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagerId =
        Guid.Parse("c1000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId =
        Guid.Parse("c1000000-0000-0000-0000-000000000003");
    public static readonly Guid SuperAdminId =
        Guid.Parse("c1000000-0000-0000-0000-000000000004");
    public const string OwnerEmail = "demo-owner@example.com";
    public const string ManagerEmail = "demo-manager@example.com";
    public const string MemberEmail = "demo-member@example.com";
    public const string SuperAdminEmail = "demo-superadmin@example.com";
    public const string Password = "TaskoraDemo123!";

    /// <summary>
    /// Whether a super-admin administrative-bypass action (delete/suspend)
    /// should be honored for the given actor against the given target
    /// workspace. Always true for a real super admin. For the known public
    /// demo Super Admin persona specifically, true only when the target is
    /// the demo's own workspace - this is what stops a public visitor using
    /// that persona from permanently deleting or suspending a real
    /// workspace via the cross-tenant admin bypass.
    /// </summary>
    public static bool AllowsDestructiveBypass(Guid actingUserId, Guid targetWorkspaceId) =>
        actingUserId != SuperAdminId || targetWorkspaceId == WorkspaceId;
}
