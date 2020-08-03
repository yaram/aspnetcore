using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebApplication
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = "./TestDb1.db" };
            services.AddDbContext<BlogContext>(options => options.UseSqlite(connectionStringBuilder.ConnectionString));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var blogContext = context.RequestServices.GetService<BlogContext>();

                    //blogContext.Blogs.Add(new Blog { Name = "TestBlog" });
                    //blogContext.SaveChanges();
                    //blogContext.Blogs.Add(new Blog { Name = "TestBlog", Name2 = "Subname" });
                    //blogContext.SaveChanges();

                    var blogs = blogContext.Blogs.Select(b => $"{b.Name}:{b.Name2}:{b.Name3}").ToList().Aggregate((a, b) => $"{a}, {b}");

                    await context.Response.WriteAsync($"Hello World! {blogs}");
                });
            });
        }
    }
}
