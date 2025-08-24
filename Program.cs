using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- CORS: allow your GitHub Pages site ---
var frontendOrigin = builder.Configuration["FrontendOrigin"]
                     ?? "https://<your-username>.github.io/vetnest-launch";
builder.Services.AddCors(o =>
    o.AddPolicy("web", p => p.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod()));

// --- DB: SQLite ---
builder.Services.AddDbContext<VetNestContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Db")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("web");
app.UseSwagger();
app.UseSwaggerUI();

// --- Create DB & seed a little data on first run ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VetNestContext>();
    db.Database.EnsureCreated(); // simple (no migrations)

    if (!db.Providers.Any())
    {
        var p = new Provider { Name = "Vet Homes GA", ContactEmail = "contact@vethomes.org", State = "GA" };
        db.Providers.Add(p);
        db.SaveChanges();

        db.Resources.AddRange(
            new ResourceItem { Type = ResourceType.Housing, Title = "Transitional Housing", State = "GA", ProviderId = p.Id, Description = "3–6 months program" },
            new ResourceItem { Type = ResourceType.Counseling, Title = "PTSD Support Group", State = "GA", ProviderId = p.Id }
        );
        db.Listings.Add(new Listing { ProviderId = p.Id, Title = "1BR Veteran Unit - Atlanta", State = "GA", MonthlyCost = 850, PetsAllowed = true, Accessible = true });
        db.SaveChanges();
    }
}

// -------- Endpoints --------

// Resources directory
app.MapGet("/api/resources", async (VetNestContext db, ResourceType? type, string? state, string? q) =>
{
    var query = db.Resources.Include(r => r.Provider).AsQueryable();
    if (type.HasValue) query = query.Where(r => r.Type == type.Value);
    if (!string.IsNullOrWhiteSpace(state)) query = query.Where(r => r.State == state);
    if (!string.IsNullOrWhiteSpace(q)) query = query.Where(r => r.Title.Contains(q) || (r.Description ?? "").Contains(q));
    return Results.Ok(await query.OrderBy(r => r.Type).ThenBy(r => r.Title).ToListAsync());
});

// Listings search
app.MapGet("/api/listings", async (VetNestContext db, string? state, bool? pets, bool? accessible, decimal? maxRent) =>
{
    var q = db.Listings.Include(l => l.Provider).AsQueryable();
    if (!string.IsNullOrWhiteSpace(state)) q = q.Where(l => l.State == state);
    if (pets.HasValue) q = q.Where(l => l.PetsAllowed == pets.Value);
    if (accessible.HasValue) q = q.Where(l => l.Accessible == accessible.Value);
    if (maxRent.HasValue) q = q.Where(l => l.MonthlyCost != null && l.MonthlyCost <= maxRent.Value);
    return Results.Ok(await q.OrderBy(l => l.MonthlyCost).ToListAsync());
});

// Submit application
app.MapPost("/api/applications", async (VetNestContext db, Application appModel) =>
{
    if (!await db.Listings.AnyAsync(l => l.Id == appModel.ListingId))
        return Results.BadRequest(new { error = "Listing not found." });

    appModel.SubmittedUtc = DateTime.UtcNow;
    db.Applications.Add(appModel);
    await db.SaveChangesAsync();
    return Results.Created($"/api/applications/{appModel.Id}", appModel);
});

app.Run();

// ===== Models + DbContext (kept here to keep it simple) =====
public enum ResourceType { Housing, Benefits, Counseling, Employment }

public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? State { get; set; }
}

public class ResourceItem
{
    public int Id { get; set; }
    public ResourceType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? State { get; set; }
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
}

public class Listing
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string State { get; set; } = "GA";
    public decimal? MonthlyCost { get; set; }
    public bool PetsAllowed { get; set; }
    public bool Accessible { get; set; }
}

public class Application
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
}

public class VetNestContext : DbContext
{
    public VetNestContext(DbContextOptions<VetNestContext> options) : base(options) { }
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ResourceItem> Resources => Set<ResourceItem>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Application> Applications => Set<Application>();
}
