namespace BlogApi;

public class PostRepository : IPostRepository
{
    private readonly List<BlogPost> _posts = [];

    public void Add(BlogPost post) => _posts.Add(post);

    public void Clear() => _posts.Clear();

    /// Returns only published posts.
    public IReadOnlyList<BlogPost> GetAll() =>
        _posts.Where(p => p.IsPublished).ToList();

    /// Returns a post by ID — but only if it is published.
    /// This is the business rule that the mediator mock will bypass.
    public BlogPost? GetById(string id) =>
        _posts.FirstOrDefault(p => p.Id == id && p.IsPublished);
}
