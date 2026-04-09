namespace BlogApi;

public interface IPostRepository
{
    void Add(BlogPost post);
    void Clear();
    IReadOnlyList<BlogPost> GetAll();
    BlogPost? GetById(string id);
}
