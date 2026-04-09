import { PactV4, MatchersV3 } from "@pact-foundation/pact";
import path from "path";
import { getPosts, blogApi } from "../src/api";

const { eachLike, string, boolean, like } = MatchersV3;

const provider = new PactV4({
  consumer: "blog-frontend",
  provider: "blog-api",
  dir: path.resolve(__dirname, "..", "pacts"),
});

describe("GetPosts endpoint contract tests", () => {
  it("returns a list of published posts", async () => {
    await provider
      .addInteraction()
      .given("published posts exist")
      .uponReceiving("a GET request to /api/posts")
      .withRequest("GET", "/api/posts")
      .willRespondWith(200, (_builder) => {
        _builder.jsonBody(
          eachLike({
            id: string("post-1"),
            title: string("My First Post"),
            content: string("Hello world"),
            author: string("Alice"),
            isPublished: boolean(true),
            createdAt: like("2025-01-01T00:00:00Z"),
          })
        );
      })
      .executeTest(async (mockServer) => {
        blogApi.defaults.baseURL = `${mockServer.url}/api`;
        const posts = await getPosts();
        expect(posts).toHaveLength(1);
        expect(posts[0].title).toBe("My First Post");
      });
  });
});
