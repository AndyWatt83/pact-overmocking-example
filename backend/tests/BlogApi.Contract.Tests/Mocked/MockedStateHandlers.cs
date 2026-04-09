using BlogApi.Contract.Tests.ProviderStates;
using Moq;

namespace BlogApi.Contract.Tests.Mocked;

/// <summary>
/// Provider state handlers that configure a Moq mock of IPostRepository.
/// The mock bypasses all business logic — it returns whatever you tell it to,
/// regardless of what the real repository would do.
/// </summary>
public class MockedStateHandlers(Mock<IPostRepository> repoMock) : IProviderStateHandler
{
    [ProviderState(ProviderStateNames.PublishedPostsExist)]
    public void PublishedPostsExist()
    {
        repoMock.Reset();
        repoMock.Setup(r => r.GetAll()).Returns([
            new BlogPost("post-1", "My First Post", "Hello world", "Alice", true, DateTime.Parse("2025-01-01"))
        ]);
    }

    [ProviderState(ProviderStateNames.PostExistsForGivenId)]
    public void PostExistsForGivenId()
    {
        repoMock.Reset();
        // The mock returns the draft post directly — it never checks IsPublished.
        // This is the over-mock: the real PostRepository.GetById filters out drafts.
        repoMock.Setup(r => r.GetById("draft-post-1")).Returns(
            new BlogPost("draft-post-1", "My Draft Post", "Work in progress", "Alice", false, DateTime.Parse("2025-01-01"))
        );
    }
}
