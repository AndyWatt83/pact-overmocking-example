using BlogApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPostRepository, PostRepository>();

var app = builder.Build();

app.MapGet("/api/posts", (IPostRepository repo) =>
    Results.Ok(repo.GetAll()));

app.MapGet("/api/posts/{id}", (string id, IPostRepository repo) =>
{
    var post = repo.GetById(id);
    return post is not null ? Results.Ok(post) : Results.NotFound();
});

app.Run();
