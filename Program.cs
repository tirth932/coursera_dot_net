var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Global exception handler middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var errorResponse = new { message = ex.Message, detail = ex.StackTrace };
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

// Authentication middleware
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
    if (token == null || !ValidateToken(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
        return;
    }
    await next();
});

// Logging middleware
app.Use(async (context, next) =>
{
    // Log request
    var request = context.Request;
    Console.WriteLine($"Incoming Request: {request.Method} {request.Path}");

    // Capture the original response body stream
    var originalBodyStream = context.Response.Body;

    using (var responseBody = new MemoryStream())
    {
        context.Response.Body = responseBody;

        await next();

        // Log response
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        Console.WriteLine($"Outgoing Response: {context.Response.StatusCode} {responseText}");

        await responseBody.CopyToAsync(originalBodyStream);
    }

    context.Response.Body = originalBodyStream; // Ensure the original stream is restored
});

// Add a new endpoint to test the global exception handler
app.MapGet("/api/test-exception", () =>
{
    throw new Exception("This is a test exception.");
});

var users = new List<User>();
var userDictionary = new Dictionary<int, User>();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/users", () => Results.Ok(users));

app.MapGet("/api/users/{id}", (int id) =>
{
    if (id <= 0)
    {
        return Results.BadRequest("Invalid user ID.");
    }

    if (userDictionary.TryGetValue(id, out var user))
    {
        return Results.Ok(user);
    }
    return Results.NotFound();
});

app.MapPost("/api/users", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.Name))
    {
        return Results.BadRequest("Name is required.");
    }

    if (!IsValidEmail(user.Email))
    {
        return Results.BadRequest("Invalid email format.");
    }

    user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
    users.Add(user);
    userDictionary[user.Id] = user;
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapPut("/api/users/{id}", (int id, User updatedUser) =>
{
    if (userDictionary.TryGetValue(id, out var user))
    {
        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;
        return Results.NoContent();
    }
    return Results.NotFound();
});

app.MapDelete("/api/users/{id}", (int id) =>
{
    if (userDictionary.TryGetValue(id, out var user))
    {
        users.Remove(user);
        userDictionary.Remove(id);
        return Results.NoContent();
    }
    return Results.NotFound();
});

app.Run();

// Helper method to validate email format
bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

// Helper method to validate token
bool ValidateToken(string token)
{
    // Implement your token validation logic here
    return token == "valid-token"; // Example: Replace with actual validation
}

public class User
{
    public int Id { get; set; }
    required public string Name { get; set; }
    required public string Email { get; set; }
}
