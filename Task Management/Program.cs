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
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
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

    // 3. SEEDING DATA WITH BOGUS
    // NOTE: SuperAdmin (Id = 1) is already seeded via TMDbContext.OnModelCreating HasData.
    //       Here we only add the 10,000 regular fake users.
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<TMDbContext>();

        // <= 1 means only the SuperAdmin from HasData exists — no fake users yet
        if (context.Users.Count() < 1)
        {
            Log.Information("Seeding 10,000 fake users via Bogus...");

            // FIX: Use BCrypt.Net — same library used by TMDbContext and AuthService.
            //      Hash the password ONCE and reuse the string for all 10,000 users.
            //      Hashing once vs 10,000 times = seeding completes in seconds not minutes.
            const string commonPwd = "Test@123";
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(commonPwd);

            // SuperAdmin is excluded — only Admin and User roles assigned by Bogus
            var userFaker = new Faker<User>()
                .RuleFor(u => u.Username, f => f.Internet.UserName().ToLower())
                .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Username))
                .RuleFor(u => u.PasswordHash, _ => hashedPassword) // Same BCrypt hash reused
                .RuleFor(u => u.IsDeleted, _ => false)
                .RuleFor(u => u.Role, f => f.PickRandom(UserRole.Admin, UserRole.User))
                .RuleFor(u => u.CreatedAt, f => f.Date.Past(1));

            // Increase EF command timeout to 10 minutes for bulk inserts
            context.Database.SetCommandTimeout(600);

            const int batchSize = 1000;
            const int total = 10000;

            for (int i = 0; i < total; i += batchSize)
            {
                var batch = userFaker.Generate(Math.Min(batchSize, total - i));
                context.Users.AddRange(batch);
                context.SaveChanges();
                Log.Information("Seeded {Done}/{Total} users...", i + batch.Count, total);
            }

            Log.Information("Seeding complete! All 10,000 users — Password: {Pwd}", commonPwd);
        }
        else
        {
            Log.Information("Users table already has data. Skipping Bogus seed.");
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 4. LOGGING MIDDLEWARE (Tracks speed/time for all APIs)
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseRouting();

    // Consistency Middleware for 401/403
    app.Use(async (context, next) =>
    {
        await next();
        if (context.Response.StatusCode == 401 && !context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Authentication failed: No token provided or token is invalid."
            });
        }
        else if (context.Response.StatusCode == 403 && !context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
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