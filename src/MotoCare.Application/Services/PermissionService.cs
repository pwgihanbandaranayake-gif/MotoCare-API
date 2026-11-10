using Microsoft.Extensions.Caching.Memory;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Application.Masters;

namespace MotoCare.Application.Services;

public interface IPermissionService
{
    /// <summary>True when the role may do the action (View/Add/Edit/Delete/Approve) on the module. Admin can do everything.</summary>
    Task<bool> IsAllowedAsync(int roleId, string roleName, string module, string action);

    /// <summary>Limits of the role: max discount, can give credit, can void, ...</summary>
    Task<RoleLimits> GetLimitsAsync(int roleId);

    /// <summary>Clears the cache after permissions or limits are changed.</summary>
    void Invalidate();
}

/// <summary>Permission checks with a short memory cache so every request does not hit the database.</summary>
public sealed class PermissionService(IUserRepository users, IMasterRepository masters, IMemoryCache cache) : IPermissionService
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(2);
    private static int _version;   // bumping the version makes every old cache key unused

    public async Task<bool> IsAllowedAsync(int roleId, string roleName, string module, string action)
    {
        if (roleName == "Admin") return true;

        var key = $"perm:{_version}:{roleId}:{module}:{action}";
        if (cache.TryGetValue(key, out bool allowed)) return allowed;

        allowed = await users.CheckPermissionAsync(roleId, module, action);
        cache.Set(key, allowed, CacheTime);
        return allowed;
    }

    public async Task<RoleLimits> GetLimitsAsync(int roleId)
    {
        var key = $"limits:{_version}:{roleId}";
        if (cache.TryGetValue(key, out RoleLimits? limits) && limits is not null) return limits;

        var role = await masters.GetAsync(MasterRegistry.All["roles"], roleId)
                   ?? throw new KeyNotFoundException("User type not found.");

        limits = new RoleLimits(
            IsAdmin: role.Get<string>("Name") == "Admin",
            MaxDiscountPct: role.Get<decimal>("MaxDiscountPct"),
            CanOverrideCredit: role.Get<bool>("CanOverrideCredit"),
            CanVoidInvoice: role.Get<bool>("CanVoidInvoice"),
            CanEditPrice: role.Get<bool>("CanEditPrice"),
            CanGiveCredit: role.Get<bool>("CanGiveCredit"));

        cache.Set(key, limits, CacheTime);
        return limits;
    }

    public void Invalidate() => Interlocked.Increment(ref _version);
}
