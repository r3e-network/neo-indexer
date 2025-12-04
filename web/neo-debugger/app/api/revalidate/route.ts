import { NextResponse } from "next/server";
import { revalidatePath } from "next/cache";

export async function POST(request: Request) {
  const authHeader = request.headers.get("authorization");
  const token = process.env.REVALIDATE_TOKEN;

  if (!token || authHeader !== `Bearer ${token}`) {
    return NextResponse.json({ error: "unauthorized" }, { status: 401 });
  }

  const { path = "/" } = await request.json().catch(() => ({ path: "/" }));
  revalidatePath(path);
  return NextResponse.json({ revalidated: true, path });
}
