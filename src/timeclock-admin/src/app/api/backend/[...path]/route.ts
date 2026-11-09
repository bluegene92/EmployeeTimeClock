import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext) {
  const apiUrl = (process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5229").replace(/\/$/, "");
  const apiKey = process.env.ADMIN_API_KEY?.trim();
  let upstreamOrigin: URL;
  try {
    upstreamOrigin = new URL(apiUrl);
  } catch {
    return NextResponse.json({ message: "API_URL is not a valid URL." }, { status: 500 });
  }

  const isLocal = upstreamOrigin.hostname === "localhost" || upstreamOrigin.hostname === "127.0.0.1";
  if (!apiKey && !isLocal) {
    return NextResponse.json({ message: "ADMIN_API_KEY is not configured." }, { status: 500 });
  }

  const { path } = await context.params;
  const target = new URL(`/${path.join("/")}`, upstreamOrigin);
  target.search = request.nextUrl.search;

  const headers = new Headers();
  const contentType = request.headers.get("content-type");
  if (contentType) headers.set("content-type", contentType);
  if (apiKey) headers.set("X-Admin-Api-Key", apiKey);

  const hasBody = request.method !== "GET" && request.method !== "HEAD";
  let upstream: Response;
  try {
    upstream = await fetch(target, {
      method: request.method,
      headers,
      body: hasBody ? await request.arrayBuffer() : undefined,
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ message: "The time clock API could not be reached." }, { status: 502 });
  }

  const responseHeaders = new Headers();
  const upstreamType = upstream.headers.get("content-type");
  if (upstreamType) responseHeaders.set("content-type", upstreamType);
  return new NextResponse(await upstream.arrayBuffer(), {
    status: upstream.status,
    headers: responseHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
