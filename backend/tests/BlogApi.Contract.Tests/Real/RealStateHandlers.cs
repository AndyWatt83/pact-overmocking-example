using BlogApi.Contract.Tests.ProviderStates;

namespace BlogApi.Contract.Tests.Real;

/// <summary>
/// Provider state handlers that seed data into the real PostRepository.
/// The actual endpoint logic runs — including the IsPublished filter in GetById.
/// </summary>
public class RealStateHandlers(IPostRepository repository) : IProviderStateHandler
{
    [ProviderState(ProviderStateNames.PublishedPostsExist)]
    public void PublishedPostsExist()
    {
        repository.Clear();
        repository.Add(new BlogPost("post-1", "My First Post", "Hello world", "Alice", true, DateTime.Parse("2025-01-01")));
    }

    [ProviderState(ProviderStateNames.PostExistsForGivenId)]
    public void PostExistsForGivenId()
    {
        repository.Clear();
        // Seed a DRAFT post — the real PostRepository.GetById filters by IsPublished,
        // so this post will NOT be returned. The pact will fail with 404.
        repository.Add(new BlogPost("draft-post-1", "My Draft Post", "Work in progress", "Alice", false, DateTime.Parse("2025-01-01")));
    }
}
