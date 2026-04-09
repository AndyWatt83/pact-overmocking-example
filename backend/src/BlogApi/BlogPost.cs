namespace BlogApi;

public record BlogPost(string Id, string Title, string Content, string Author, bool IsPublished, DateTime CreatedAt);
