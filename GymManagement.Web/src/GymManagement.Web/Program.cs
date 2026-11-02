using GymManagement.Application.Services;
using GymManagement.Infrastructure;
using GymManagement.Infrastructure.Data;
using GymManagement.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Infrastructure, Multi-tenant, Caching, and Cryptography services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Production Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("System", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Gym Management Cloud Platform Online"));

var app = builder.Build();

// Seed initial database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
    await DbInitializer.SeedAsync(db);
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Map Health Checks Endpoint
app.MapHealthChecks("/health");

// -------------------------------------------------------------
// Mobile App REST API Endpoints (Geofencing & Workout Duration)
// -------------------------------------------------------------
var mobileApi = app.MapGroup("/api/v1/mobile");

// 1. Get branch geofences for Mobile App to register with OS
mobileApi.MapGet("/geofences", async (IMembershipService memberService) =>
{
    var geofences = await memberService.GetMobileGeofencesAsync();
    return Results.Ok(new
    {
        Success = true,
        Count = geofences.Count,
        Data = geofences
    });
});

// 2. Mobile App Enter Geofence -> Auto Check-In
mobileApi.MapPost("/geofence/check-in", async (
    [Microsoft.AspNetCore.Mvc.FromBody] GymManagement.Application.DTOs.GeofenceCheckInRequestDto req,
    IMembershipService memberService) =>
{
    var result = await memberService.ProcessGeofenceCheckInAsync(
        req.MemberCodeOrPhone, 
        req.Latitude, 
        req.Longitude, 
        req.TargetBranchId);

    return result.Success 
        ? Results.Ok(result) 
        : Results.BadRequest(result);
});

// 3. Mobile App Exit Geofence -> Auto Check-Out & Duration Record
mobileApi.MapPost("/geofence/check-out", async (
    [Microsoft.AspNetCore.Mvc.FromBody] GymManagement.Application.DTOs.GeofenceCheckInRequestDto req,
    IMembershipService memberService) =>
{
    var result = await memberService.ProcessGeofenceCheckOutAsync(
        req.MemberCodeOrPhone, 
        req.Latitude, 
        req.Longitude, 
        req.TargetBranchId);

    return result.Success 
        ? Results.Ok(result) 
        : Results.BadRequest(result);
});

// 4. Member Profile & Active Session Status
mobileApi.MapGet("/members/{codeOrPhone}", async (string codeOrPhone, IMembershipService memberService) =>
{
    var members = await memberService.GetMembersAsync(codeOrPhone);
    var member = members.FirstOrDefault(m => m.MemberCode.Equals(codeOrPhone, StringComparison.OrdinalIgnoreCase) || m.PhoneNumber == codeOrPhone);
    if (member == null) return Results.NotFound(new { Success = false, Message = "Member not found" });

    var activeSession = await memberService.GetActiveCheckInSessionAsync(member.Id);
    var history = await memberService.GetMemberCheckInsHistoryAsync(member.Id);

    return Results.Ok(new
    {
        Success = true,
        Member = member,
        ActiveSession = activeSession != null ? new
        {
            activeSession.Id,
            activeSession.BranchId,
            BranchName = activeSession.Branch?.BranchName,
            activeSession.CheckInTime,
            Method = activeSession.Method.ToString()
        } : null,
        History = history.Select(h => new
        {
            h.Id,
            h.CheckInTime,
            h.CheckOutTime,
            BranchName = h.Branch?.BranchName,
            Status = h.Status.ToString(),
            Method = h.Method.ToString(),
            h.DistanceMeters,
            DurationMinutes = h.DurationMinutes ?? 0
        })
    });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
