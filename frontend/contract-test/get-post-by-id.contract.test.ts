import { PactV4, MatchersV3 } from "@pact-foundation/pact";
import path from "path";
import { getPostById, blogApi } from "../src/api";

const { string, boolean, like } = MatchersV3;

const provider = new PactV4({
  consumer: "blog-frontend",
  provider: "blog-api",
  dir: path.resolve(__dirname, "..", "pacts"),
});

describe("GetPostById endpoint contract tests", () => {
  it("returns a single post by ID", async () => {
    const postId = "draft-post-1";

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
      .executeTest(async (mockServer) => {
        blogApi.defaults.baseURL = `${mockServer.url}/api`;
        const post = await getPostById(postId);
        expect(post.id).toBe(postId);
        expect(post.title).toBe("My Draft Post");
      });
  });
});
