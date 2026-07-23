/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: 'standalone',
  // DEC-110: Image optimization config
  // No <img> tags currently in app (uses lucide-react icons), but config ready for future
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'Anas-Assasket-erp-system.hf.space',
      },
    ],
    formats: ['image/avif', 'image/webp'],
  },
  // v1.0.18: Proxy /api/* to backend so pages can use relative fetch('/api/...')
  // API_URL_INTERNAL: hostname of API service reachable from the frontend container/process
  //   - In Docker: http://api:5000
  //   - Local dev (host): http://localhost:5000
  async rewrites() {
    const apiUrl = process.env.API_URL_INTERNAL || 'http://localhost:5000';
    return [
      { source: '/api/:path*', destination: `${apiUrl}/api/:path*` },
    ];
  },
};
module.exports = nextConfig;
