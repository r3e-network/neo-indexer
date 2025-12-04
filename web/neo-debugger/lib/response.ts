import { NextResponse } from "next/server";

export function jsonWithCache(data: any, seconds = 15) {
  const res = NextResponse.json(data);
  res.headers.set("Cache-Control", `public, s-maxage=${seconds}, stale-while-revalidate=${seconds}`);
  return res;
}
