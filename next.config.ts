import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  cacheComponents: true,
  partialPrefetching: true,
  async headers() {
    return [
      {
        // Always treat files in /downloads as a download (never render inline), with a
        // stable file name and a correct type, and don't let a stale copy get cached.
        source: "/downloads/:file*",
        headers: [
          // No filename here, so each file keeps its own name from the URL.
          { key: "Content-Disposition", value: "attachment" },
          { key: "Content-Type", value: "application/zip" },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Cache-Control", value: "no-cache" },
        ],
      },
    ];
  },
};

export default nextConfig;
