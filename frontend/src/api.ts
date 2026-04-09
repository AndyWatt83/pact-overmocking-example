import axios from "axios";

export const blogApi = axios.create({
  baseURL: "http://localhost:5000/api",
  timeout: 10000,
});

export async function getPosts(): Promise<BlogPost[]> {
  const response = await blogApi.get<BlogPost[]>("/posts");
  return response.data;
}

export async function getPostById(id: string): Promise<BlogPost> {
  const response = await blogApi.get<BlogPost>(`/posts/${id}`);
  return response.data;
}

export interface BlogPost {
  id: string;
  title: string;
  content: string;
  author: string;
  isPublished: boolean;
  createdAt: string;
}
