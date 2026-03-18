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
using Sieve.Services;

// ─────────────────────────────────────────────
// 1. BOOTSTRAP LOGGER (used before host builds)
// ─────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Log.Information("  Task Management Web API — Starting Up");
    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var builder = WebApplication.CreateBuilder(args);

    // ─────────────────────────────────────────────
    // 2. SERILOG — APP HOST CONFIGURATION
    // ─────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .MinimumLevel.Information()

        // Suppress internal ASP.NET + EF Core noise — these flood the log file
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)

        .Enrich.FromLogContext()

        // Console — short and readable (developer-friendly)
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")

        // File — includes date, rolls daily, keeps 7 days
        .WriteTo.File(
            path: "logs/task_management_.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")

        // Allow appsettings.json to override any level at runtime
        .ReadFrom.Configuration(ctx.Configuration));
    builder.Services.AddApplicationInsightsTelemetry();
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

    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<ICommentService, CommentService>();
    builder.Services.AddScoped<ISieveProcessor, SieveProcessor>();

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
        Log.Information("[Migration] Applying pending migrations...");
        await context.Database.MigrateAsync();
        Log.Information("[Migration] Migrations applied successfully.");

        // STEP 2: Seed SuperAdmin ONLY if not already present
        bool superAdminExists = await context.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);

        if (!superAdminExists)
        {
            Log.Information("[Seed] SuperAdmin not found — seeding...");

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

            Log.Information("[Seed] SuperAdmin created | Email: superadmin@taskmanager.com | Password: SuperAdmin@123");
        }
        else
        {
            Log.Information("[Seed] SuperAdmin already exists — skipping.");
        }

        // STEP 3: Reseed identity counter
        Log.Information("[DB] Reseeding Users identity counter...");
        await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Users', RESEED, 1)");
        Log.Information("[DB] Identity counter reset to 1.");

        // STEP 4: Seed 600,000 fake users ONLY if they don't already exist
        bool fakeUsersExist = await context.Users.AnyAsync(u => u.Role != UserRole.SuperAdmin);

        if (!fakeUsersExist)
        {
            Log.Information("[Seed] No fake users found — seeding 600,000 users...");

            const string commonPwd = "Test@123";
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(commonPwd);

            var userFaker = new Faker<User>()
                .RuleFor(u => u.Username, f => f.Internet.UserName().ToLower() + f.UniqueIndex)
                .RuleFor(u => u.Email, (f, u) => $"{u.Username}@taskmanager.com")
                .RuleFor(u => u.PasswordHash, _ => hashedPassword)
                .RuleFor(u => u.IsDeleted, _ => false)
                .RuleFor(u => u.Role, f => f.PickRandom(UserRole.Admin, UserRole.User))
                .RuleFor(u => u.CreatedAt, f => f.Date.Past(1));

            context.Database.SetCommandTimeout(600);
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            const int userBatchSize = 2000;
            const int totalUsers = 10_000;

            for (int i = 0; i < totalUsers; i += userBatchSize)
            {
                var batch = userFaker.Generate(Math.Min(userBatchSize, totalUsers - i));
                context.Users.AddRange(batch);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                // Log progress every 50,000 users to avoid log spam
                if ((i + batch.Count) % 50_000 == 0 || (i + batch.Count) == totalUsers)
                    Log.Information("[Seed] Users: {Done:N0} / {Total:N0}", i + batch.Count, totalUsers);
            }

            context.ChangeTracker.AutoDetectChangesEnabled = true;
            Log.Information("[Seed] User seeding complete — 600,000 users added. Common password: {Pwd}", commonPwd);
        }
        else
        {
            Log.Information("[Seed] Fake users already exist — skipping user seed.");
        }

        // STEP 5: Seed 200,000 tasks ONLY if they don't already exist
        bool tasksExist = await context.Tasks.AnyAsync();

        if (!tasksExist)
        {
            Log.Information("[Seed] No tasks found — seeding 200,000 tasks...");
            Log.Information("[Seed] Fetching Admin user IDs...");

            var adminIds = await context.Users
                .Where(u => u.Role == UserRole.Admin && !u.IsDeleted)
                .Select(u => u.Id)
                .Take(200_000)
                .ToListAsync();

            if (adminIds.Count == 0)
            {
                Log.Warning("[Seed] No Admin users found — skipping task seed.");
            }
            else
            {
                Log.Information("[Seed] Found {Count:N0} admins — shuffling for unique assignment...", adminIds.Count);

                var rng = new Random();
                adminIds = adminIds.OrderBy(_ => rng.Next()).ToList();

                Log.Information("[Seed] Shuffle complete — starting task seeding...");

                var faker = new Faker();

                var taskStatuses = new[]
                {
                    TM.Model.Entities.TaskStatus.Pending,
                    TM.Model.Entities.TaskStatus.InProgress,
                    TM.Model.Entities.TaskStatus.Completed
                };

                context.ChangeTracker.AutoDetectChangesEnabled = false;
                context.Database.SetCommandTimeout(600);

                const int taskBatchSize = 2000;
                const int totalTasks = 200_000;

                for (int i = 0; i < totalTasks; i += taskBatchSize)
                {
                    int currentBatch = Math.Min(taskBatchSize, totalTasks - i);
                    var tasks = new List<TM.Model.Entities.Task>(currentBatch);

                    for (int j = 0; j < currentBatch; j++)
                    {
                        var adminId = adminIds[i + j];

                        tasks.Add(new TM.Model.Entities.Task
                        {
                            Title = faker.Lorem.Sentence(4),
                            Description = faker.Lorem.Paragraph(),
                            Status = faker.PickRandom(taskStatuses),
                            DueDate = faker.Date.Future(1),
                            CreatedBy = adminId,
                            AssignedToUserId = null,
                            IsDeleted = false
                        });
                    }

                    context.Tasks.AddRange(tasks);
                    await context.SaveChangesAsync();
                    context.ChangeTracker.Clear();

                    // Log progress every 50,000 tasks to avoid log spam
                    if ((i + currentBatch) % 50_000 == 0 || (i + currentBatch) == totalTasks)
                        Log.Information("[Seed] Tasks: {Done:N0} / {Total:N0}", i + currentBatch, totalTasks);
                }

                context.ChangeTracker.AutoDetectChangesEnabled = true;
                Log.Information("[Seed] Task seeding complete — 200,000 tasks added. Each assigned a unique admin. All unassigned.");
            }
        }
        else
        {
            Log.Information("[Seed] Tasks already exist — skipping task seed.");
        }
    }

    // ─────────────────────────────────────────────
    // MIDDLEWARE PIPELINE
    // ─────────────────────────────────────────────

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskManagementAPI v1");
        c.RoutePrefix = "swagger";
    });

    // 3. SERILOG REQUEST LOGGING — one clean line per HTTP request
    app.UseSerilogRequestLogging(options =>
    {
        // Format: HTTP GET /api/tasks → 200 in 14.2ms
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";

        // 4xx = Warning (yellow), 5xx or exception = Error (red), rest = Info
        options.GetLevel = (httpContext, elapsed, ex) =>
            ex != null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseHttpsRedirection();
    app.UseRouting();

    // Consistent JSON body for 401 / 403
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

    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Log.Information("  Task Management Web API — Running");
    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "[FATAL] Application start-up failed.");
}
finally
{
    Log.CloseAndFlush();
}