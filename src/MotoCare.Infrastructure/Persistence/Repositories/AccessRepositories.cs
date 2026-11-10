using Dapper;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;
using MotoCare.Application.Masters;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Login, users, permissions, master files, settings and dashboard.
// Each method = one stored procedure call. No business rules here.

public sealed class AuthRepository(DbExecutor db) : IAuthRepository
{
    public Task<LoginUserRecord?> GetForLoginAsync(string username) =>
        db.FirstAsync<LoginUserRecord>("dbo.sp_User_GetForLogin", new { Username = username });

    public Task TouchLoginAsync(int userId) =>
        db.ExecAsync("dbo.sp_User_TouchLogin", new { UserId = userId });
}

public sealed class UserRepository(DbExecutor db) : IUserRepository
{
    public Task<List<RowData>> ListAsync() => db.ListAsync("dbo.sp_User_List");

    public Task<int> SaveAsync(int userId, string username, string? passwordHash, int? employeeId, int roleId, bool isActive) =>
        db.SaveAsync("dbo.sp_User_Save", new
        {
            UserId = userId, Username = username, PasswordHash = passwordHash,
            EmployeeId = employeeId, RoleId = roleId, IsActive = isActive
        });

    public Task<List<RowData>> GetPermissionsAsync(int roleId) =>
        db.ListAsync("dbo.sp_RolePermission_Get", new { RoleId = roleId });

    public Task SavePermissionsAsync(int roleId, IEnumerable<PermissionDto> permissions) =>
        db.ExecAsync("dbo.sp_RolePermission_Save", new { RoleId = roleId, Items = TableTypes.Permissions(permissions) });

    public async Task<bool> CheckPermissionAsync(int roleId, string module, string action) =>
        await db.ScalarAsync<bool>("dbo.sp_Permission_Check", new { RoleId = roleId, ModuleKey = module, Action = action });
}

public sealed class MasterRepository(DbExecutor db) : IMasterRepository
{
    public Task<List<RowData>> ListAsync(MasterDefinition def, string? search, bool activeOnly, int? parentId, string? filter) =>
        db.ListAsync(def.ListProc, new { Search = search, ActiveOnly = activeOnly, ParentId = parentId, Filter = filter });

    public Task<RowData?> GetAsync(MasterDefinition def, int id) =>
        db.SingleAsync(def.GetProc, new { Id = id });

    public Task<int> SaveAsync(MasterDefinition def, int id, IReadOnlyDictionary<string, object?> values, bool isActive)
    {
        // parameter names come from the master definition: @Id, @<each field>, @IsActive
        var p = new DynamicParameters();
        p.Add("Id", id);
        foreach (var (name, value) in values) p.Add(name, value);
        p.Add("IsActive", isActive);
        return db.SaveAsync(def.SaveProc, p);
    }

    public async Task<string> DeleteAsync(MasterDefinition def, int id) =>
        await db.ScalarAsync<string>(def.DeleteProc, new { Id = id }) ?? "Deleted";
}

public sealed class SettingsRepository(DbExecutor db) : ISettingsRepository
{
    public Task<List<RowData>> ListAsync() => db.ListAsync("dbo.sp_Setting_List");

    public Task SaveAsync(string key, string value) =>
        db.ExecAsync("dbo.sp_Setting_Save", new { SettingKey = key, SettingValue = value });

    public Task<List<RowData>> DocNumbersAsync() => db.ListAsync("dbo.sp_DocNumber_List");

    public Task SaveDocNumberAsync(DocNumberDto d) =>
        db.ExecAsync("dbo.sp_DocNumber_Save", new { d.DocType, d.Prefix, d.NextNo, d.Padding });
}

public sealed class DashboardRepository(DbExecutor db) : IDashboardRepository
{
    public Task<ResultBag> GetAsync() =>
        db.MultiAsync("dbo.sp_Dashboard_Get", null, "kpis?", "appointments", "lowStock", "recentInvoices", "reminders");
}
