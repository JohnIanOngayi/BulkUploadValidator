
using BulkUploadValidator.Repository;
using BulkUploadValidator.Services;
using Scalar.AspNetCore;

namespace BulkUploadValidator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<ILinkRepository, LinkRepository>();
            builder.Services.AddScoped<ISiteRepository, SiteRepository>();

            builder.Services.AddScoped<TemplateGenerator<SiteTemplateConfig>>();
            builder.Services.AddScoped<TemplateGenerator<LinkTemplateConfig>>();

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            //builder.Services.AddScalar();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
