using ForecastApp1.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<PaymentScheduleService>();
builder.Services.AddScoped<Func<NpgsqlConnection>>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return () => new NpgsqlConnection(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapGet("/", () => Results.Redirect("index.html"));
app.UseRouting();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();


app.Run();
