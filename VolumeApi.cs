// ============================================================
//  Irregular Solid Volume Calculator — C# ASP.NET Core API
//  Disk/Washer Method + Composite Simpson's Rule
//  Exposes: POST /api/volume  →  JSON result
// ============================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── CORS (allow the webapp to call us) ──────────────────────
builder.Services.AddCors(o => o.AddPolicy("Open", p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors("Open");

// ── Catalogue of irregular solids ───────────────────────────
var solids = new List<SolidDef>
{
    new("Sinusoidal Paraboloid Shell",
        "A parabolic solid modulated by a sine wave, producing a ribbed shell-like surface with periodic undulations along its length.",
        "r(x) = R·(1−(x/L)²) + A·sin(N·π·x/L)",
        new[]{"Ribbed","Shell","Organic"},
        (x, L, R, A, N) => R * (1 - Math.Pow(x / L, 2)) + A * Math.Sin(N * Math.PI * x / L)),

    new("Damped-Wave Frustum",
        "A cone-like frustum whose radius decays with an oscillating cosine envelope — mimicking a vibrating body settling to rest.",
        "r(x) = R + (A−R)·(x/L) + N·cos(3x)·e^(−0.3x)",
        new[]{"Acoustic","Frustum","Decaying"},
        (x, L, R, A, N) => Math.Max(0, R + (A - R) * (x / L) + N * Math.Cos(3 * x) * Math.Exp(-0.3 * x))),

    new("Exponential Ogive Dome",
        "A dome whose profile follows an exponential ogive curve — wide base tapering smoothly to a rounded apex, common in ballistics.",
        "r(x) = R·√(1−(x/L)²)·e^(−A·x/L)",
        new[]{"Dome","Ogive","Ballistic"},
        (x, L, R, A, N) => R * Math.Sqrt(Math.Max(0, 1 - Math.Pow(x / L, 2))) * Math.Exp(-A * x / L)),

    new("Hyperbolic Annular Solid",
        "A solid bounded by a hyperbolic curve — narrower at the waist and flared symmetrically at both ends.",
        "r(x) = √(R² + (x−L/2)²/A²)",
        new[]{"Hyperboloid","Ring","Flared"},
        (x, L, R, A, N) => Math.Sqrt(R * R + Math.Pow(x - L / 2, 2) / (A * A))),

    new("Polynomial Biconcave Spindle",
        "A lens-shaped solid following a degree-4 polynomial — concave at both ends and convex at the centre, like a red blood cell.",
        "r(x) = R·[4·(x/L)·(1−x/L)]^N",
        new[]{"Biconcave","Lens","Cell-like"},
        (x, L, R, A, N) => { double b = 4 * (x / L) * (1 - x / L); return b > 0 ? R * Math.Pow(b, N) : 0; })
};

// ── POST /api/volume ─────────────────────────────────────────
app.MapPost("/api/volume", async (HttpContext ctx) =>
{
    VolumeRequest? req;
    try
    {
        req = await JsonSerializer.DeserializeAsync<VolumeRequest>(
            ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("{\"error\":\"Invalid JSON body\"}");
        return;
    }

    if (req is null || req.SolidIndex < 0 || req.SolidIndex >= solids.Count)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("{\"error\":\"solidIndex out of range\"}");
        return;
    }

    if (req.L <= 0 || req.R <= 0)
    {
        ctx.Response.StatusCode = 422;
        await ctx.Response.WriteAsync("{\"error\":\"L and R must be positive\"}");
        return;
    }

    var solid = solids[req.SolidIndex];
    double volume = Simpson(solid.RadiusFn, req.L, req.R, req.A, req.N, 1000);

    var result = new VolumeResult(
        SolidName  : solid.Name,
        Volume     : Math.Round(volume, 6),
        Unit       : "cm³",
        Method     : "Disk/Washer + Composite Simpson's Rule",
        Slices     : 1000,
        DeltaX     : req.L / 1000,
        Language   : "C# (ASP.NET Core Minimal API)"
    );

    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
});

// ── GET /api/solids — list all solid definitions ─────────────
app.MapGet("/api/solids", () =>
{
    var list = solids.Select((s, i) => new
    {
        index   = i,
        name    = s.Name,
        desc    = s.Desc,
        formula = s.Formula,
        tags    = s.Tags
    });
    return Results.Json(list);
});

// ── GET /api/health ──────────────────────────────────────────
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", language = "C#" }));

app.Run();

// ── Composite Simpson's Rule ─────────────────────────────────
static double Simpson(Func<double,double,double,double,double,double> fn,
                      double L, double R, double A, double N, int n = 1000)
{
    if (n % 2 != 0) n++;          // Simpson needs even n
    double h = L / n;
    double sum = 0;
    for (int i = 0; i <= n; i++)
    {
        double x  = i * h;
        double r  = fn(x, L, R, A, N);
        double f  = Math.PI * r * r;
        double c  = (i == 0 || i == n) ? 1 : (i % 2 == 1 ? 4 : 2);
        sum += c * f;
    }
    return sum * h / 3;
}

// ── Record types ─────────────────────────────────────────────
record SolidDef(
    string Name,
    string Desc,
    string Formula,
    string[] Tags,
    Func<double,double,double,double,double,double> RadiusFn);

record VolumeRequest(
    int    SolidIndex,
    double L,
    double R,
    double A,
    double N);

record VolumeResult(
    string SolidName,
    double Volume,
    string Unit,
    string Method,
    int    Slices,
    double DeltaX,
    string Language);
