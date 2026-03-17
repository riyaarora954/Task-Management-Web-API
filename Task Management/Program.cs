using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using Bogus;
using TM.Model.Data;
using TM.Model.Entities;
using TM.ServiceLogic.Implementations;
using TM.ServiceLogic.Interfaces;
using TM.ServiceLogic.Mappings;

// 1. Setup Initial Bootstrap Logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Task Management Web API...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Configure Serilog for the App Host
    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .WriteTo.File("logs/task_management_log.txt", rollingInterval: RollingInterval.Day)
        .ReadFrom.Configuration(ctx.Configuration));

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(opt =>
    {
        opt.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskManagementAPI", Version = "v1" });
        opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter token in format: Bearer {your_token}",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "bearer"
        });
        opt.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] { }
            }
        });
    });

    builder.Services.AddDbContext<TMDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Dependency Injection
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<ICommentService, CommentService>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
            };
        });

    builder.Services.AddAutoMapper(
        cfg => { cfg.AddProfile<MappingProfile>(); },
        AppDomain.CurrentDomain.GetAssemblies());

    var app = builder.Build();

    // ─────────────────────────────────────────────
    // DATABASE SEEDING
    // ─────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<TMDbContext>();

        // STEP 1: Apply pending migrations
        Log.Information("Applying pending migrations...");
        await context.Database.MigrateAsync();
        Log.Information("Migrations applied successfully.");

        // STEP 2: Seed SuperAdmin ONLY if not already present
        bool superAdminExists = await context.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);

        if (!superAdminExists)
        {
            Log.Information("Seeding SuperAdmin user...");

            var superAdmin = new User
            {
                Username = "superadmin",
                Email = "superadmin@taskmanager.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin@123"),
                IsDeleted = false,
                Role = UserRole.SuperAdmin,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(superAdmin);
            await context.SaveChangesAsync();

            Log.Information("SuperAdmin seeded. Email: superadmin@taskmanager.com | Password: SuperAdmin@123");
        }
        else
        {
            Log.Information("SuperAdmin already exists. Skipping.");
        }

        // STEP 3: Reseed identity counter so fake users follow SuperAdmin's Id
        // This ensures the next inserted row gets the correct next Id
        Log.Information("Reseeding identity counter...");
        await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Users', RESEED, 1)");
        Log.Information("Identity counter reset successfully.");

        // STEP 4: Seed 10,000 fake users ONLY if they don't already exist
        bool fakeUsersExist = await context.Users.AnyAsync(u => u.Role != UserRole.SuperAdmin);

        if (!fakeUsersExist)
        {
            Log.Information("Seeding 10,000 fake users via Bogus...");

            // Hash password ONCE and reuse — saves enormous time vs hashing 10,000 times
            const string commonPwd = "Test@123";
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(commonPwd);

            // Only Admin and User roles assigned to fake users
            var userFaker = new Faker<User>()
                .RuleFor(u => u.Username, f => f.Internet.UserName().ToLower())
                .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Username))
                .RuleFor(u => u.PasswordHash, _ => hashedPassword)
                .RuleFor(u => u.IsDeleted, _ => false)
                .RuleFor(u => u.Role, f => f.PickRandom(UserRole.Admin, UserRole.User))
                .RuleFor(u => u.CreatedAt, f => f.Date.Past(1));

            // Increase EF command timeout to 10 minutes for bulk inserts
            context.Database.SetCommandTimeout(600);

            const int batchSize = 1000;
            const int total = 10_000;

            for (int i = 0; i < total; i += batchSize)
            {
                var batch = userFaker.Generate(Math.Min(batchSize, total - i));
                context.Users.AddRange(batch);
                await context.SaveChangesAsync();
                Log.Information("Seeded {Done}/{Total} fake users...", i + batch.Count, total);
            }

            Log.Information("Seeding complete! 10,000 fake users added. Password: {Pwd}", commonPwd);
        }
        else
        {
            Log.Information("Fake users already exist. Skipping Bogus seed.");
        }
    }

    // ─────────────────────────────────────────────
    // MIDDLEWARE PIPELINE
    // ─────────────────────────────────────────────

    // Always enable Swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskManagementAPI v1");
        c.RoutePrefix = "swagger"; // Access at: https://localhost:{port}/swagger/index.html
    });

    // Serilog Request Logging Middleware
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseRouting();

    // Consistency Middleware for 401 / 403 responses
    app.Use(async (httpContext, next) =>
    {
        await next();

        if (httpContext.Response.StatusCode == 401 && !httpContext.Response.HasStarted)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                message = "Authentication failed: No token provided or token is invalid."
            });
        }
        else if (httpContext.Response.StatusCode == 403 && !httpContext.Response.HasStarted)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                message = "Access Denied: You do not have the required permissions."
            });
        }
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed.");
}
finally
{
    Log.CloseAndFlush();
}