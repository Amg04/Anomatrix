using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.ServiceContracts;
using Deadlock.Core.Services;
using Deadlock.Infrastructure.Data.DbContext;
using Deadlock.Infrastructure.Data.DBInitializer;
using Deadlock.Infrastructure.Email;
using Deadlock.Infrastructure.Repositories;
using Deadlock.Infrastructure.Services;
using Deadlock.Infrastructure.Services.Hubs;
using Deadlock.UI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Deadlock.UI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddDbContext<DriftersDBContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString("CS"));
            });

            builder.Services.AddIdentity<AppUser, IdentityRole>()
              .AddEntityFrameworkStores<DriftersDBContext>()
              .AddDefaultTokenProviders();


            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IDBInitializer, DBInitializer>();
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
            builder.Services.AddSignalR();

            builder.Services.AddSingleton<ICameraStreamService, CameraStreamService>();
            builder.Services.AddSingleton<ICameraProcessManager, FfmpegProcessService>();
            builder.Services.AddSingleton<IRtspUrlBuilder, RtspUrlBuilder>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddHostedService<RtspPumpService>();
            builder.Services.AddTransient<IJwtService, JwtService>();

            builder.Services.AddDataProtection();

            #region JWT

            builder.Services.AddAuthentication(Options =>
            {
                Options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                Options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                Options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(Options =>
            {
                Options.SaveToken = true;
                Options.RequireHttpsMetadata = false;
                Options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])),
                    ClockSkew = TimeSpan.Zero
                };

                Options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine("JWT ERROR: " + context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        Console.WriteLine("JWT CHALLENGE ERROR: " + context.ErrorDescription);
                        return Task.CompletedTask;
                    }
                };
            });

            #endregion

            #region Swagger

            builder.Services.AddSwaggerGen(swagger =>
            {
                swagger.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Deedlock",
                    Version = "v1",
                    Description = "API FOR SW"
                });

                swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your?valid token in the text input below.Example:Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9",
                });
                swagger.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                    new string[] {}
                    }
                    });
            });

            #endregion

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Deedlock API V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHub<CameraHub>("/cameraHub");
            await SeedDatabaseAsync();

            app.MapControllers();

            app.Run();

            async Task SeedDatabaseAsync()
            {
                using (var scope = app.Services.CreateScope())
                {
                    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
                    await dbInitializer.Initialize();
                }
            }
        }
    }
}
