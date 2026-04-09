# Pact Over-Mocking Example

This repo demonstrates how mocking at the wrong level in Pact provider tests can hide real bugs. Two provider verification modes are included side by side — one that **passes** (mocked) and one that **fails** (real) — against the same consumer pact.

## The Setup

A simple blogging app with a Vite/TypeScript frontend and a .NET Minimal API backend.

**Endpoints:**
- `GET /api/posts` — list posts
- `GET /api/posts/{id}` — get a single post by ID

**The business rule:** `PostRepository.GetById()` only returns **published** posts. Draft posts are filtered out.

**The consumer expectation:** the frontend expects `GET /api/posts/draft-post-1` to return a post — but that post is a draft.

## The Bug

The consumer pact says: "when I ask for post `draft-post-1`, I expect a 200 with the post body."

The real API returns **404** because the repository filters out unpublished posts.

This is a genuine contract mismatch. The consumer and provider disagree on what happens when you request a draft post by ID.

## How It Works

### The production code

The repository has a business rule — `GetById` only returns **published** posts:

```csharp
// backend/src/BlogApi/PostRepository.cs
public BlogPost? GetById(string id) =>
    _posts.FirstOrDefault(p => p.Id == id && p.IsPublished);
```

The endpoint calls this directly:

```csharp
// backend/src/BlogApi/Program.cs
app.MapGet("/api/posts/{id}", (string id, IPostRepository repo) =>
{
    var post = repo.GetById(id);
    return post is not null ? Results.Ok(post) : Results.NotFound();
});
```

### The consumer pact

The frontend test expects to fetch a **draft** post (`isPublished: false`) by ID and get a 200:

```typescript
// frontend/contract-test/get-post-by-id.contract.test.ts
await provider
  .addInteraction()
  .given("a post exists for the given ID")
  .uponReceiving("a GET request to /api/posts/{id}")
  .withRequest("GET", `/api/posts/${postId}`)
  .willRespondWith(200, (_builder) => {
    _builder.jsonBody({
      id: string(postId),
      title: string("My Draft Post"),
      content: string("Work in progress"),
      author: string("Alice"),
      isPublished: boolean(false),
      createdAt: like("2025-01-01T00:00:00Z"),
    });
  })
```

This is a real bug — the consumer thinks it can fetch drafts, but the API filters them out.

### Mocked provider state (hides the bug)

```csharp
// backend/tests/BlogApi.Contract.Tests/Mocked/MockedStateHandlers.cs
[ProviderState("a post exists for the given ID")]
public void PostExistsForGivenId()
{
    repoMock.Reset();
    // The mock returns the draft post directly — it never checks IsPublished.
    repoMock.Setup(r => r.GetById("draft-post-1")).Returns(
        new BlogPost("draft-post-1", "My Draft Post", "Work in progress",
                     "Alice", false, DateTime.Parse("2025-01-01"))
    );
}
```

The mock says "when someone calls `GetById("draft-post-1")`, return this draft post." The **real filtering logic never runs**. The endpoint receives the post, returns 200. Pact passes.

### Real provider state (catches the bug)

```csharp
// backend/tests/BlogApi.Contract.Tests/Real/RealStateHandlers.cs
[ProviderState("a post exists for the given ID")]
public void PostExistsForGivenId()
{
    repository.Clear();
    // Seed a DRAFT post — the real PostRepository.GetById filters by IsPublished,
    // so this post will NOT be returned. The pact will fail with 404.
    repository.Add(new BlogPost("draft-post-1", "My Draft Post", "Work in progress",
                                "Alice", false, DateTime.Parse("2025-01-01")));
}
```

The real repo gets the draft post added. But when the endpoint calls `repository.GetById("draft-post-1")`, the real `IsPublished` filter kicks in, returns `null`, and the endpoint returns **404**. Pact expected 200 — **fails**.

### Side by side

Both approaches seed the **exact same data** — a draft post with `IsPublished = false`. The difference is what happens when the endpoint asks for it:

| | Mock | Real |
|---|---|---|
| `GetById("draft-post-1")` | Returns the draft (mock bypasses filter) | Returns `null` (filter rejects it) |
| Endpoint response | 200 + post body | 404 |
| Pact result | **Pass** | **Fail** |

The mock replaces the boundary where the business logic lives, so the business logic is invisible to the test. The real approach lets the business logic execute, and the bug is caught.

## Two Provider Verification Modes

### Mocked (passes — bug hidden)

```bash
PROVIDER_TEST_MODE=mocked dotnet test backend/tests/BlogApi.Contract.Tests
```

### Real (fails — bug exposed)

```bash
PROVIDER_TEST_MODE=real dotnet test backend/tests/BlogApi.Contract.Tests
```

## Why This Matters

The mock returns whatever you tell it to. It never executes business logic — filtering, validation, authorisation, data transformation. When you mock at this level, the pact verifies the **shape of your test data**, not the **behaviour of your API**.

The pact file is the source of truth for `can-i-deploy`. If the provider verification doesn't exercise the real code path, the gate provides false confidence.

## "But Isn't This More Than Contract Testing?"

A reasonable response to this example is: "the mock-based test is doing its job — it verifies the request and response shapes match. The business logic bug is the responsibility of integration tests, not contract tests."

That's a fair perspective, and it's true that contract testing originated as a way to catch serialisation mismatches between services. But consider what the contract actually promises. The consumer pact doesn't just say "the response has these fields." It says: **"when I send this request, I get this status code with this body."** The 200 status code is part of the contract. If the provider actually returns 404, the contract is broken — even if the JSON shapes are perfectly compatible.

The mocked approach verifies that the endpoint *can* serialise the expected response type. It doesn't verify that the endpoint *would* produce that response for the given request. Those are different questions, and `can-i-deploy` needs the answer to the second one.

The practical concern is that `can-i-deploy` is a deployment gate. It makes a binary decision based on pact verification results. If the verification uses mocks that bypass real behaviour, the gate can say "safe to deploy" when it isn't. Integration tests might catch the same bug — but they don't feed into the broker, and they don't block the deployment pipeline.

This doesn't mean every provider test needs a full production-like environment. It means the verification should exercise enough of the real code path that the status codes and response shapes in the pact reflect what the API would actually return. Where you draw that line is a judgement call — but drawing it *above* the business logic means the business logic is invisible to the contract.

This isn't a novel position — it's what the Pact project itself recommends.

## What Pact Themselves Recommend

The official Pact documentation is explicit about where to draw the stubbing boundary during provider verification.

### Only stub beneath request extraction

From [Verifying Pacts](https://docs.pact.io/provider):

> **"Only stub layers beneath where contents of the request body are extracted."**
>
> "If you don't have to stub anything in the Provider when running pact:verify, then don't. If you do need to stub something (eg. a downstream system), make sure that you only stub the code that gets executed **after** the contents of the request body have been extracted and validated. Otherwise, you could send any old garbage in a POST or PUT body, and no test would fail."

In the blogging example, mocking `IPostRepository` stubs out the layer *where* the request is processed — not beneath it. The business rule (filter by `IsPublished`) sits inside the repository, and the mock replaces it entirely.

### Verify against a real, locally running instance

Also from [Verifying Pacts](https://docs.pact.io/provider):

> "Pact is designed to give you confidence that your integration is working correctly before you deploy either application. To achieve this, the verification step must be run against a **locally running instance** of your provider."

### Provider states should catch false positives

From [Using provider states effectively](https://docs.pact.io/provider/using_provider_states_effectively):

> "The purpose of contract testing is to ensure that the consumer and provider have a **shared understanding** of the messages that will pass between them."

The docs then walk through a detailed example where a search endpoint ignores an incorrect query parameter, the provider state only seeds one matching record, and the test passes — giving a **false positive**. The recommended fix is to seed data that would expose the bug: add extra records so that if the filtering doesn't work, the test fails.

This is exactly the same principle at play in our example. The mocked provider state returns a result that bypasses the filter. The real provider state seeds data that the filter acts on — and the bug is caught.

### Stub downstream services, not your own logic

From Beth Skurrie's [conceptual background gist](https://gist.github.com/bethesque/43eef1bf47afea4445c8b8bdebf28df0) (linked from the Pact docs):

> "When you verify the A/B Pact, it's best to stub out the calls to C at some level."

The guidance is to stub *external dependencies* (other services, databases) — not your own application logic. The repository in our example isn't a downstream service. It's the provider's own business logic.

## Reproducing

### Prerequisites

- Node.js 18+
- .NET 10 SDK

### 1. Generate the consumer pact

```bash
cd frontend
npm install
npm run test:contract
```

This produces `frontend/pacts/blog-frontend-blog-api.json`.

### 2. Run the mocked provider verification (passes)

```bash
PROVIDER_TEST_MODE=mocked dotnet test backend/tests/BlogApi.Contract.Tests
```

### 3. Run the real provider verification (fails)

```bash
PROVIDER_TEST_MODE=real dotnet test backend/tests/BlogApi.Contract.Tests
```

## Project Structure

```
├── frontend/
│   ├── src/api.ts                          # Real service functions
│   ├── contract-test/
│   │   ├── get-posts.contract.test.ts      # Consumer pact test
│   │   └── get-post-by-id.contract.test.ts # Consumer pact test (the one that exposes the bug)
│   └── pacts/                              # Generated pact JSON
└── backend/
    ├── src/BlogApi/
    │   ├── Program.cs                      # Minimal API endpoints
    │   ├── BlogPost.cs                     # Domain record
    │   ├── IPostRepository.cs              # Interface
    │   └── PostRepository.cs               # In-memory repo (filters by IsPublished)
    └── tests/BlogApi.Contract.Tests/
        ├── ContractVerificationTests.cs    # Reads PROVIDER_TEST_MODE env var
        ├── Mocked/
        │   ├── MockedHostBuilder.cs        # Builds host with Moq mock
        │   └── MockedStateHandlers.cs      # mock.Setup returns canned data
        └── Real/
            ├── RealHostBuilder.cs          # Builds host with real PostRepository
            └── RealStateHandlers.cs        # repository.Add(draftPost)
```

## The Takeaway

If your provider verification mocks the layer that contains business logic, it can't catch bugs in that logic. The pact will pass, the deployment gate will open, and the bug will reach production.

Verify against the real code path. Let your tests find the bugs your mocks are hiding.
